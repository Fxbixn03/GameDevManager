using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Assets;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Wohin exportiert wird. Der Inhalt ist für alle Ziele derselbe — JSON je Modul plus die
/// Asset-Dateien, alle Verweise als GUIDs. Das Ziel bestimmt nur, unter welchem Wurzelpfad
/// die Dateien im ZIP liegen (damit sich das Archiv direkt ins Engine-Projekt entpacken
/// lässt) und welche Hinweise die README enthält.
/// </summary>
public enum ExportTarget
{
    Json = 0,
    Unity = 1,
    Unreal = 2,
    Godot = 3
}

/// <summary>
/// Der Export des Konzepts: der komplette Stand eines Projekts als einfaches JSON zusammen
/// mit den Assets als ZIP — oder in der Ordnerstruktur einer Engine (Unity, Unreal, Godot).
/// <para>
/// Serialisiert werden die Domain-Entitäten selbst, allerdings ohne Navigationsobjekte:
/// Referenzen laufen laut Konzept ausschließlich über GUIDs, und genau so stehen sie auch
/// in den Dateien. Kind-Sammlungen (Rezept-Zutaten, Händler-Posten, Dialogzeilen, …) bleiben
/// eingebettet, weil sie ohne ihren Besitzer nichts bedeuten.
/// </para>
/// <para>
/// Alle Listen sind stabil sortiert (Name bzw. SortOrder, dann GUID). Damit ist derselbe
/// Stand Byte für Byte derselbe Export — die Grundlage für die versionierten, diffbaren
/// Exporte, die das Konzept verlangt.
/// </para>
/// </summary>
public class ExportService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
    EngineExportWriter engineWriter,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Version des Exportformats, steht im Manifest (<c>project.json</c>). Wird bei jeder
    /// Änderung an Aufbau oder Bedeutung der Dateien erhöht, damit der spätere Import und
    /// die Engine-Seite wissen, was sie vor sich haben.
    /// </summary>
    /// <remarks>
    /// Version 2: Arten tragen eine <c>parentId</c> — Unterarten erben die Felder ihrer
    /// Eltern-Art. Version 4: Karten-Markierungen tragen <c>points</c> — Gebiete als Polygon
    /// statt nur als Kreis. Version 5: NPCs tragen Beziehungen samt <c>relationTypes</c> in
    /// <c>content/npcs.json</c> sowie Einzigartig-Schalter, Vorlieben, Persönlichkeit und
    /// Wesenszüge; Karten tragen <c>layers</c>, Markierungen eine <c>layerId</c>;
    /// Story-Abschnitte tragen Stimmung, Spieldatum, Dauer, Ort, Karten-Verknüpfung
    /// und <c>links</c> auf andere Abschnitte. Version 6: Felddefinitionen tragen
    /// <c>isTagList</c> — Textfelder mit mehreren Stichwörtern, deren Wert kommagetrennt in
    /// <c>content/field-values.json</c> steht.
    /// </remarks>
    public const int FormatVersion = 7;

    /// <summary>
    /// Schreibt den kompletten Projektstand als ZIP nach <paramref name="output"/>.
    /// <para>
    /// Das Archiv entsteht erst in einer temporären Datei und wird dann kopiert: ZipArchive
    /// schließt seine Einträge synchron ab, und der Response-Stream von ASP.NET Core lässt
    /// synchrone Schreibzugriffe nicht zu. Die Datei löscht sich über <c>DeleteOnClose</c>
    /// von selbst, auch wenn der Download abbricht.
    /// </para>
    /// </summary>
    public async Task WriteExportAsync(
        Guid projectId, ExportTarget target, bool includeAssets, Stream output, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"gdm-export-{Guid.NewGuid():N}.zip");
        await using var temp = new FileStream(
            tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        await BuildArchiveAsync(projectId, target, includeAssets, temp, ct);

        temp.Position = 0;
        await temp.CopyToAsync(output, ct);
    }

    private async Task BuildArchiveAsync(
        Guid projectId, ExportTarget target, bool includeAssets, Stream zipStream, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var missingProject = messages["Export_ProjectMissing"].Value;
        var project = await db.GameProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException(missingProject);

        // ------------------------------------------------------------------ Inhalte laden
        async Task<List<T>> LoadContentAsync<T>(IQueryable<T> query) where T : ContentEntity =>
            await query.AsNoTracking()
                .Where(e => e.GameProjectId == projectId)
                .OrderBy(e => e.Name).ThenBy(e => e.Id)
                .ToListAsync(ct);

        var items = await LoadContentAsync(db.Items);
        var recipes = await LoadContentAsync(db.Recipes
            .Include(r => r.Outputs).Include(r => r.Ingredients));
        var currencies = await LoadContentAsync(db.Currencies);
        var rarities = await LoadContentAsync(db.Rarities);
        var npcs = await LoadContentAsync(db.Npcs.Include(n => n.Offers).Include(n => n.Relations));
        var factions = await LoadContentAsync(db.Factions.Include(f => f.Members));
        var relations = await LoadContentAsync(db.DiplomaticRelations);
        var maps = await LoadContentAsync(db.Maps.Include(m => m.Markers).Include(m => m.Layers));
        var dialogues = await LoadContentAsync(db.Dialogues
            .Include(d => d.Participants)
            .Include(d => d.Lines).ThenInclude(l => l.Choices));
        var quests = await LoadContentAsync(db.Quests);
        var events = await LoadContentAsync(db.GameEvents.Include(e => e.Spawns));
        var skills = await LoadContentAsync(db.Skills);
        var classes = await LoadContentAsync(db.CharacterClasses);
        var effects = await LoadContentAsync(db.GameEffects.Include(e => e.Assignments));
        var achievements = await LoadContentAsync(db.Achievements);
        var collectibles = await LoadContentAsync(db.Collectibles);
        var soundEffects = await LoadContentAsync(db.SoundEffects);
        var cutscenes = await LoadContentAsync(db.Cutscenes.Include(c => c.Shots));
        var lootTables = await LoadContentAsync(db.LootTables.Include(t => t.Entries));
        var worldStates = await LoadContentAsync(db.WorldStates);

        // Der Zeitstreifen ist eine Reihenfolge — hier sortiert sie statt des Namens.
        var storyEntries = await db.StoryEntries.AsNoTracking()
            .Include(s => s.Participants)
            .Include(s => s.Links)
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync(ct);

        var players = await db.PlayerCharacters.AsNoTracking()
            .Where(p => p.GameProjectId == projectId)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var skillTrees = await db.SkillTrees.AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var contentTypes = await db.ContentTypes.AsNoTracking()
            .Include(t => t.Fields).ThenInclude(f => f.Options)
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.ModuleKey).ThenBy(t => t.SortOrder).ThenBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var conditionSets = await db.ConditionSets.AsNoTracking()
            .Include(s => s.Conditions)
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.OwnerModuleKey).ThenBy(s => s.OwnerId).ThenBy(s => s.Slot)
            .ToListAsync(ct);

        var contentTags = await db.ContentTags.AsNoTracking()
            .Include(t => t.Scopes).Include(t => t.Assignments)
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var languages = await db.ContentLanguages.AsNoTracking()
            .Where(l => l.GameProjectId == projectId)
            .OrderByDescending(l => l.IsSource).ThenBy(l => l.SortOrder).ThenBy(l => l.Code)
            .ToListAsync(ct);

        var translations = await db.ContentTranslations.AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.LanguageCode).ThenBy(t => t.OwnerModuleKey)
            .ThenBy(t => t.OwnerEntityId).ThenBy(t => t.Slot)
            .ToListAsync(ct);

        var enginePresets = await db.EnginePresets.AsNoTracking()
            .Include(preset => preset.Mappings)
            .Where(preset => preset.GameProjectId == projectId)
            .OrderBy(preset => preset.Engine).ThenBy(preset => preset.SortOrder)
            .ThenBy(preset => preset.Name).ThenBy(preset => preset.Id)
            .ToListAsync(ct);

        var assetTags = await db.AssetTags.AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var npcRelationTypes = await db.NpcRelationTypes.AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var assets = await db.Assets.AsNoTracking()
            .Include(a => a.Tags)
            .Where(a => a.GameProjectId == projectId)
            .OrderBy(a => a.FileName).ThenBy(a => a.Id)
            .ToListAsync(ct);

        // Feldwerte und individuelle Felder tragen keine Projekt-Spalte — sie hängen über die
        // GUID an ihrer Entität. Gefiltert wird deshalb über die Menge aller exportierten GUIDs.
        List<ContentEntity>[] contentLists =
        [
            [.. items], [.. recipes], [.. currencies], [.. rarities], [.. npcs], [.. factions],
            [.. relations], [.. maps], [.. dialogues], [.. storyEntries], [.. quests], [.. events],
            [.. skills], [.. classes], [.. effects], [.. achievements], [.. collectibles],
            [.. soundEffects], [.. cutscenes], [.. lootTables]
        ];
        var entityIds = contentLists.SelectMany(list => list).Select(e => e.Id)
            .Concat(players.Select(p => p.Id))
            .Concat(skillTrees.Select(t => t.Id))
            .ToHashSet();

        var individualFields = (await db.FieldDefinitions.AsNoTracking()
                .Include(f => f.Options)
                .Where(f => f.OwnerEntityId != null)
                .OrderBy(f => f.ModuleKey).ThenBy(f => f.SortOrder).ThenBy(f => f.Name).ThenBy(f => f.Id)
                .ToListAsync(ct))
            .Where(f => entityIds.Contains(f.OwnerEntityId!.Value))
            .ToList();

        var fieldValues = (await db.FieldValues.AsNoTracking()
                .OrderBy(v => v.OwnerModuleKey).ThenBy(v => v.OwnerEntityId).ThenBy(v => v.FieldDefinitionId)
                .ToListAsync(ct))
            .Where(v => entityIds.Contains(v.OwnerEntityId))
            .ToList();

        // ------------------------------------------------- Kind-Sammlungen stabil sortieren
        recipes.ForEach(r =>
        {
            r.Outputs = [.. r.Outputs.OrderBy(o => o.SortOrder).ThenBy(o => o.Id)];
            r.Ingredients = [.. r.Ingredients.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)];
        });
        npcs.ForEach(n =>
        {
            n.Offers = [.. n.Offers.OrderBy(o => o.SortOrder).ThenBy(o => o.Id)];
            n.Relations = [.. n.Relations.OrderBy(r => r.SortOrder).ThenBy(r => r.Id)];
        });
        factions.ForEach(f => f.Members = [.. f.Members.OrderBy(m => m.SortOrder).ThenBy(m => m.Id)]);
        maps.ForEach(m =>
        {
            m.Markers = [.. m.Markers.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)];
            m.Layers = [.. m.Layers.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)];
        });
        dialogues.ForEach(d =>
        {
            d.Participants = [.. d.Participants.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)];
            d.Lines = [.. d.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.Id)];
            d.Lines.ForEach(l => l.Choices = [.. l.Choices.OrderBy(c => c.SortOrder).ThenBy(c => c.Id)]);
        });
        storyEntries.ForEach(s =>
        {
            s.Participants = [.. s.Participants.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)];
            s.Links = [.. s.Links.OrderBy(l => l.SortOrder).ThenBy(l => l.Id)];
        });
        events.ForEach(e => e.Spawns = [.. e.Spawns.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)]);
        effects.ForEach(e => e.Assignments = [.. e.Assignments.OrderBy(a => a.SortOrder).ThenBy(a => a.Id)]);
        cutscenes.ForEach(c => c.Shots = [.. c.Shots.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)]);
        lootTables.ForEach(t => t.Entries = [.. t.Entries.OrderBy(e => e.SortOrder).ThenBy(e => e.Id)]);
        contentTypes.ForEach(t =>
        {
            t.Fields = [.. t.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ThenBy(f => f.Id)];
            t.Fields.ForEach(f => f.Options = [.. f.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id)]);
        });
        individualFields.ForEach(f => f.Options = [.. f.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id)]);
        conditionSets.ForEach(s => s.Conditions = [.. s.Conditions.OrderBy(c => c.SortOrder).ThenBy(c => c.Id)]);
        contentTags.ForEach(t =>
        {
            t.Scopes = [.. t.Scopes.OrderBy(s => s.ModuleKey).ThenBy(s => s.Id)];
            t.Assignments = [.. t.Assignments.OrderBy(a => a.TargetModuleKey).ThenBy(a => a.TargetEntityId).ThenBy(a => a.Id)];
        });
        assets.ForEach(a => a.Tags = [.. a.Tags.OrderBy(t => t.AssetTagId)]);
        enginePresets.ForEach(p => p.Mappings = [.. p.Mappings.OrderBy(m => m.SortOrder).ThenBy(m => m.Id)]);

        // ------------------------------------------------------------------ ZIP schreiben
        var prefix = target switch
        {
            ExportTarget.Unity => "Assets/StreamingAssets/GameDevManager/",
            ExportTarget.Unreal => "Content/GameDevManager/",
            ExportTarget.Godot => "gamedevmanager/",
            _ => string.Empty
        };

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

        async Task WriteJsonAsync(string path, object payload)
        {
            var entry = archive.CreateEntry(prefix + path, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await JsonSerializer.SerializeAsync(entryStream, payload, ExportFormat.JsonOptions, ct);
        }

        await WriteJsonAsync("content/items.json", new { items });
        await WriteJsonAsync("content/crafting.json", new { recipes });
        await WriteJsonAsync("content/currencies.json", new { currencies });
        await WriteJsonAsync("content/rarities.json", new { rarities });
        await WriteJsonAsync("content/npcs.json", new { npcs, relationTypes = npcRelationTypes });
        await WriteJsonAsync("content/factions.json", new { factions });
        await WriteJsonAsync("content/diplomacy.json", new { relations });
        await WriteJsonAsync("content/maps.json", new { maps });
        await WriteJsonAsync("content/dialogs.json", new { dialogues });
        await WriteJsonAsync("content/story.json", new { entries = storyEntries });
        await WriteJsonAsync("content/quests.json", new { quests });
        await WriteJsonAsync("content/events.json", new { events });
        await WriteJsonAsync("content/player.json", new { playerCharacters = players, skillTrees, skills });
        await WriteJsonAsync("content/classes.json", new { classes });
        await WriteJsonAsync("content/loot.json", new { lootTables });
        await WriteJsonAsync("content/world.json", new { worldStates });
        await WriteJsonAsync("content/effects.json", new { effects });
        await WriteJsonAsync("content/achievements.json", new { achievements });
        await WriteJsonAsync("content/collectibles.json", new { collectibles });
        await WriteJsonAsync("content/audio.json", new { soundEffects });
        await WriteJsonAsync("content/cutscenes.json", new { cutscenes });
        await WriteJsonAsync("content/tags.json", new { tags = contentTags });
        await WriteJsonAsync("content/localization.json", new { languages, translations });
        await WriteJsonAsync("content/engine-presets.json", new { presets = enginePresets });
        await WriteJsonAsync("content/types-and-fields.json", new { contentTypes, individualFields });
        await WriteJsonAsync("content/field-values.json", new { values = fieldValues });
        await WriteJsonAsync("content/conditions.json", new { conditionSets });
        await WriteJsonAsync("content/assets.json", new { assetTags, assets });

        // Je Sprache eine fertige Zeichenketten-Tabelle unter „localization/“. Sie ist
        // vollständig aus content/localization.json abgeleitet und steht trotzdem daneben:
        // Eine Engine lädt zur Laufzeit eine Sprache, und sie soll dafür nicht den gesamten
        // Übersetzungsbestand durchsuchen müssen. Die Sprachwahl fällt damit dort, wo sie
        // hingehört — im Spiel, nicht im Export.
        foreach (var language in languages.Where(language => !language.IsSource))
        {
            var table = translations
                .Where(t => t.LanguageCode == language.Code)
                .ToDictionary(t => $"{t.OwnerEntityId:N}.{t.Slot}", t => t.Text);

            await WriteJsonAsync(
                $"localization/{language.Code}.json",
                new { language = language.Code, name = language.Name, strings = table });
        }

        // Die Dateien selbst; ihr Pfad im Archiv ist der StorageKey aus content/assets.json.
        var missingAssetFiles = new List<string>();

        if (includeAssets)
        {
            foreach (var asset in assets)
            {
                ct.ThrowIfCancellationRequested();

                var content = storage.OpenRead(asset.StorageKey);
                if (content is null)
                {
                    // Im Dateispeicher verschwunden — der Export bleibt vollständig lesbar,
                    // das Manifest weist die Lücke aus.
                    missingAssetFiles.Add(asset.StorageKey);
                    continue;
                }

                await using (content)
                {
                    var entry = archive.CreateEntry(prefix + "assets/files/" + asset.StorageKey, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await content.CopyToAsync(entryStream, ct);
                }
            }
        }

        var counts = new Dictionary<string, int>
        {
            [ModuleKeys.Items] = items.Count,
            [ModuleKeys.Crafting] = recipes.Count,
            [ModuleKeys.Currencies] = currencies.Count,
            [ModuleKeys.Rarities] = rarities.Count,
            [ModuleKeys.Npcs] = npcs.Count,
            [ModuleKeys.Factions] = factions.Count,
            [ModuleKeys.Diplomacy] = relations.Count,
            [ModuleKeys.Maps] = maps.Count,
            [ModuleKeys.Dialogs] = dialogues.Count,
            [ModuleKeys.Story] = storyEntries.Count,
            [ModuleKeys.Quests] = quests.Count,
            [ModuleKeys.Events] = events.Count,
            [ModuleKeys.Player] = players.Count + skillTrees.Count + skills.Count,
            [ModuleKeys.Classes] = classes.Count,
            [ModuleKeys.Loot] = lootTables.Count,
            [ModuleKeys.World] = worldStates.Count,
            [ModuleKeys.Effects] = effects.Count,
            [ModuleKeys.Achievements] = achievements.Count,
            [ModuleKeys.Collectibles] = collectibles.Count,
            [ModuleKeys.Audio] = soundEffects.Count,
            [ModuleKeys.Cutscenes] = cutscenes.Count,
            [ModuleKeys.Tags] = contentTags.Count,
            [ModuleKeys.Localization] = translations.Count,
            [ModuleKeys.EnginePresets] = enginePresets.Count,
            [ModuleKeys.Assets] = assets.Count
        };

        await WriteJsonAsync("project.json", new
        {
            formatVersion = FormatVersion,
            exportedAtUtc = DateTime.UtcNow,
            target = target.ToString().ToLowerInvariant(),
            includesAssetFiles = includeAssets,
            project = new { project.Id, project.Name, project.Description, project.CreatedAtUtc },
            counts,
            missingAssetFiles
        });

        // Die engine-nativen Dateien aus den Presets — nur beim Export in eine Engine und nur,
        // wenn es dafür Presets gibt. Sie liegen unter dem Engine-Präfix wie die Inhalte, denn
        // sie gehören ins Engine-Projekt.
        if (target != ExportTarget.Json)
        {
            var engine = target switch
            {
                ExportTarget.Unity => TargetEngine.Unity,
                ExportTarget.Unreal => TargetEngine.Unreal,
                _ => TargetEngine.Godot
            };

            foreach (var file in await engineWriter.BuildAsync(db, projectId, engine, ct))
            {
                var entry = archive.CreateEntry(prefix + file.Path, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var entryWriter = new StreamWriter(entryStream);
                await entryWriter.WriteAsync(file.Content);
            }
        }

        var readmeKey = target switch
        {
            ExportTarget.Unity => "Export_ReadmeUnity",
            ExportTarget.Unreal => "Export_ReadmeUnreal",
            ExportTarget.Godot => "Export_ReadmeGodot",
            _ => "Export_ReadmeJson"
        };
        var readme = messages["Export_ReadmeCommon", project.Name, FormatVersion].Value
            + "\n\n" + messages[readmeKey].Value + "\n";

        // Die README liegt bewusst außerhalb des Engine-Präfixes an der Wurzel des ZIPs —
        // sie beschreibt, wohin entpackt wird, und gehört nicht ins Engine-Projekt.
        var readmeEntry = archive.CreateEntry("README.md", CompressionLevel.Optimal);
        await using var readmeStream = readmeEntry.Open();
        await using var writer = new StreamWriter(readmeStream);
        await writer.WriteAsync(readme);
    }
}
