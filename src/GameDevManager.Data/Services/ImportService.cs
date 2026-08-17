using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Assets;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Ergebnis eines Imports: was angekommen ist und was dabei aufgefallen ist.</summary>
public sealed record ImportResult(
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<string> Warnings)
{
    public int TotalCount => Counts.Values.Sum();
}

/// <summary>
/// Das Gegenstück zum <see cref="ExportService"/>: liest ein Export-ZIP wieder ein, damit sich
/// ein Projekt umziehen oder aus einer Sicherung wiederherstellen lässt.
/// <para>
/// Der Import stellt immer einen <b>kompletten Projektstand</b> her, er ist kein Teil-Merge:
/// Entweder ist das Zielprojekt leer, oder sein Bestand wird vorher vollständig entfernt
/// (<c>replaceExisting</c>). Ein Merge einzelner Entitäten über GUIDs sähe harmlos aus, ließe
/// aber halbe Stände zurück — gelöschte Entitäten blieben erhalten, und der Restrict-Fremdschlüssel
/// auf die Arten machte das Ersetzen einer noch verwendeten Art unmöglich.
/// </para>
/// <para>
/// Gelesen wird mit denselben JSON-Regeln, mit denen der Export schreibt
/// (<see cref="ExportFormat.JsonOptions"/>); die <c>formatVersion</c> aus dem Manifest muss zur
/// eigenen passen. Alle GUIDs bleiben beim Import erhalten — Referenzen zwischen den Modulen
/// funktionieren dadurch ohne jedes Umschreiben. Nur die Projektzugehörigkeit wird auf das
/// Zielprojekt umgeschrieben, denn das ZIP kann aus einer anderen Installation stammen.
/// </para>
/// <para>
/// Ein <c>replaceExisting</c>-Import wirft einen bestehenden Bestand weg. Davor legt der
/// Dienst selbst einen Exportstand an (<see cref="ExportSnapshotService.CreateSafetyNetAsync"/>) —
/// hier und nicht in der Oberfläche, damit kein zweiter Aufrufer es vergessen kann.
/// </para>
/// </summary>
public class ImportService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IAssetStorage storage,
    ExportSnapshotService snapshots,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Liest das Export-ZIP aus <paramref name="zipContent"/> in das Projekt ein.
    /// Der Stream wird zuerst in eine Temp-Datei kopiert: ZipArchive braucht wahlfreien
    /// Zugriff, und der Upload-Stream aus dem Browser kann nicht springen.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        Guid projectId, Stream zipContent, bool replaceExisting, CancellationToken ct = default)
    {
        // Das Importrecht gilt nur dem Import selbst; das Duplizieren eines Projekts läuft
        // über ImportCoreAsync daran vorbei und prüft im ProjectService das Schreibrecht.
        await guard.EnsureCanImportAsync(ct);

        return await ImportCoreAsync(projectId, zipContent, replaceExisting, ct);
    }

    /// <summary>Der Import ohne Rechteprüfung — für Aufrufer, die selbst schon geprüft haben.</summary>
    internal async Task<ImportResult> ImportCoreAsync(
        Guid projectId, Stream zipContent, bool replaceExisting, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"gdm-import-{Guid.NewGuid():N}.zip");
        await using var temp = new FileStream(
            tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        await zipContent.CopyToAsync(temp, ct);
        temp.Position = 0;

        using var archive = new ZipArchive(temp, ZipArchiveMode.Read, leaveOpen: true);
        return await ImportArchiveAsync(projectId, archive, replaceExisting, ct);
    }

    private async Task<ImportResult> ImportArchiveAsync(
        Guid projectId, ZipArchive archive, bool replaceExisting, CancellationToken ct)
    {
        // ------------------------------------------------------------------ Manifest prüfen
        var manifestEntry = ExportFormat.FindManifest(archive)
            ?? throw new ContentValidationException(messages["Import_ManifestMissing"].Value);

        // Das Engine-Präfix (Unity/Unreal/Godot) hängt am Manifestpfad — alles andere liegt darunter.
        var prefix = manifestEntry.FullName[..^ExportFormat.ManifestFileName.Length];

        int formatVersion;
        bool includesAssetFiles;
        string? projectName;
        string? projectDescription;

        await using (var manifestStream = manifestEntry.Open())
        {
            using var manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: ct);
            var root = manifest.RootElement;

            formatVersion = root.TryGetProperty("formatVersion", out var version) ? version.GetInt32() : 0;
            includesAssetFiles = root.TryGetProperty("includesAssetFiles", out var flag) && flag.GetBoolean();

            var hasProject = root.TryGetProperty("project", out var projectElement);
            projectName = hasProject && projectElement.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;
            projectDescription = hasProject && projectElement.TryGetProperty("description", out var description)
                ? description.GetString()
                : null;
        }

        if (formatVersion != ExportService.FormatVersion)
        {
            throw new ContentValidationException(
                messages["Import_FormatVersionMismatch", formatVersion, ExportService.FormatVersion].Value);
        }

        // ------------------------------------------------------------------ Inhalte lesen
        async Task<T> ReadAsync<T>(string fileName) where T : new()
        {
            var entry = archive.GetEntry(prefix + ExportFormat.ContentFolder + fileName);

            // Fehlende Dateien sind leere Module — so bleibt der Import auch für ZIPs nutzbar,
            // aus denen jemand einzelne Dateien entfernt hat.
            var file = entry is null ? new T() : await ParseAsync<T>(entry);

            // Das Git-freundliche Layout legt zusätzlich einen Ordner mit einer Datei je
            // Entität an; die Sammeldatei steht dann mit leeren Listen daneben. Gelesen werden
            // beide, damit ein Archiv aus beiden Welten importierbar bleibt — und damit ein
            // von Hand ergänzter Ordner ebenso ankommt.
            var folder = prefix + ExportFormat.ContentFolder
                + Path.GetFileNameWithoutExtension(fileName) + "/";

            foreach (var part in archive.Entries
                .Where(candidate => candidate.FullName.StartsWith(folder, StringComparison.Ordinal)
                    && candidate.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal))
            {
                ExportFormat.MergeLists(file, await ParseAsync<T>(part));
            }

            return file;
        }

        async Task<T> ParseAsync<T>(ZipArchiveEntry entry) where T : new()
        {
            await using var stream = entry.Open();
            return await JsonSerializer.DeserializeAsync<T>(stream, ExportFormat.JsonOptions, ct) ?? new T();
        }

        var items = await ReadAsync<ItemsFile>("items.json");
        var crafting = await ReadAsync<CraftingFile>("crafting.json");
        var currencies = await ReadAsync<CurrenciesFile>("currencies.json");
        var rarities = await ReadAsync<RaritiesFile>("rarities.json");
        var npcs = await ReadAsync<NpcsFile>("npcs.json");
        var factions = await ReadAsync<FactionsFile>("factions.json");
        var diplomacy = await ReadAsync<DiplomacyFile>("diplomacy.json");
        var maps = await ReadAsync<MapsFile>("maps.json");
        var dialogs = await ReadAsync<DialogsFile>("dialogs.json");
        var story = await ReadAsync<StoryFile>("story.json");
        var quests = await ReadAsync<QuestsFile>("quests.json");
        var events = await ReadAsync<EventsFile>("events.json");
        var player = await ReadAsync<PlayerFile>("player.json");
        var classes = await ReadAsync<ClassesFile>("classes.json");
        var loot = await ReadAsync<LootFile>("loot.json");
        var world = await ReadAsync<WorldFile>("world.json");
        var effects = await ReadAsync<EffectsFile>("effects.json");
        var achievements = await ReadAsync<AchievementsFile>("achievements.json");
        var collectibles = await ReadAsync<CollectiblesFile>("collectibles.json");
        var audio = await ReadAsync<AudioFile>("audio.json");
        var cutscenes = await ReadAsync<CutscenesFile>("cutscenes.json");
        var tags = await ReadAsync<TagsFile>("tags.json");
        var localization = await ReadAsync<LocalizationFile>("localization.json");
        var enginePresets = await ReadAsync<EnginePresetsFile>("engine-presets.json");

        // Erst ab Formatversion 15 im Archiv — ein älterer Stand bringt schlicht keine mit.
        var exportProfiles = await ReadAsync<ExportProfilesFile>("export-profiles.json");
        var typesAndFields = await ReadAsync<TypesAndFieldsFile>("types-and-fields.json");
        var fieldValues = await ReadAsync<FieldValuesFile>("field-values.json");
        var conditions = await ReadAsync<ConditionsFile>("conditions.json");
        var assetsFile = await ReadAsync<AssetsFile>("assets.json");

        var warnings = new List<string>();

        await using var db = await factory.CreateDbContextAsync(ct);

        // Ein Import schreibt den gesamten Bestand auf einmal. Eine Protokollzeile je Entität
        // machte das Änderungsprotokoll danach unlesbar — es bekommt weiter unten stattdessen
        // einen einzigen Eintrag über den Vorgang.
        db.SuppressChangeLog = true;

        var missingProject = messages["Export_ProjectMissing"].Value;
        var project = await db.GameProjects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new ContentValidationException(missingProject);

        // ------------------------------------------------- Zielprojekt leer oder ersetzen
        var hasContent = await HasContentAsync(db, projectId, ct);

        if (!replaceExisting && hasContent)
        {
            throw new ContentValidationException(messages["Import_ProjectNotEmpty"].Value);
        }

        // Sicherheitsnetz vor dem Wipe: Der bisherige Stand bleibt als Exportstand erhalten
        // und lässt sich über denselben Import wieder einspielen. Ein leeres Zielprojekt
        // braucht keines — es gäbe nichts wiederherzustellen.
        if (replaceExisting && hasContent)
        {
            await snapshots.CreateSafetyNetAsync(projectId, ct);
        }

        // ------------------------------------------------------- Asset-Dateien einspielen
        // Vor dem Datenbank-Schreiben, damit die Zeilen gleich den endgültigen StorageKey
        // tragen. Das ist gefahrlos, auch wenn die Transaktion später scheitert: Der Inhalt
        // eines Assets ändert sich nie — dieselbe GUID hat immer dieselbe Datei.
        if (!includesAssetFiles && assetsFile.Assets.Count > 0)
        {
            warnings.Add(messages["Import_ArchiveWithoutAssetFiles"].Value);
        }

        var importedAssetKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var asset in assetsFile.Assets)
        {
            ct.ThrowIfCancellationRequested();

            var fileEntry = archive.GetEntry(prefix + ExportFormat.AssetFilesFolder + asset.StorageKey);
            if (fileEntry is null)
            {
                if (includesAssetFiles)
                {
                    warnings.Add(messages["Import_AssetFileMissing", asset.FileName].Value);
                }

                // Die Zeile kommt trotzdem an — bei einem Import in dieselbe Installation
                // liegt die Datei unter diesem Schlüssel womöglich noch im Dateispeicher.
                importedAssetKeys.Add(asset.StorageKey);
                continue;
            }

            var extension = Path.GetExtension(asset.StorageKey);
            if (string.IsNullOrEmpty(extension))
            {
                extension = Path.GetExtension(asset.FileName);
            }

            await using var content = fileEntry.Open();
            asset.StorageKey = await storage.SaveAsync(projectId, asset.Id, extension, content, ct);
            importedAssetKeys.Add(asset.StorageKey);
        }

        // ------------------------------------------------------------ Schreiben, ein Stand
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var replacedAssetKeys = new List<string>();
        if (replaceExisting)
        {
            replacedAssetKeys = await WipeProjectAsync(db, projectId, ct);
        }

        // Alle Inhalte gehören ab jetzt dem Zielprojekt — das ZIP kann aus einer anderen
        // Installation mit anderer Projekt-GUID stammen. Die Entitäts-GUIDs bleiben unberührt.
        List<ContentEntity>[] contentLists =
        [
            [.. items.Items], [.. crafting.Recipes], [.. currencies.Currencies], [.. rarities.Rarities],
            [.. npcs.Npcs], [.. factions.Factions], [.. diplomacy.Relations], [.. maps.Maps],
            [.. dialogs.Dialogues], [.. story.Entries], [.. quests.Quests], [.. events.Events],
            [.. player.Skills], [.. classes.Classes], [.. effects.Effects], [.. achievements.Achievements],
            [.. collectibles.Collectibles], [.. audio.SoundEffects], [.. cutscenes.Cutscenes],
            [.. loot.LootTables], [.. world.WorldStates]
        ];

        foreach (var entity in contentLists.SelectMany(list => list))
        {
            entity.GameProjectId = projectId;
        }

        player.PlayerCharacters.ForEach(p => p.GameProjectId = projectId);
        player.SkillTrees.ForEach(t => t.GameProjectId = projectId);
        typesAndFields.ContentTypes.ForEach(t => t.GameProjectId = projectId);
        conditions.ConditionSets.ForEach(s => s.GameProjectId = projectId);
        tags.Tags.ForEach(t => t.GameProjectId = projectId);
        localization.Languages.ForEach(l => l.GameProjectId = projectId);
        localization.Translations.ForEach(t => t.GameProjectId = projectId);
        enginePresets.Presets.ForEach(p => p.GameProjectId = projectId);
        exportProfiles.Profiles.ForEach(p => p.GameProjectId = projectId);
        npcs.RelationTypes.ForEach(t => t.GameProjectId = projectId);
        assetsFile.AssetTags.ForEach(t => t.GameProjectId = projectId);
        assetsFile.Assets.ForEach(a => a.GameProjectId = projectId);

        db.Items.AddRange(items.Items);
        db.Recipes.AddRange(crafting.Recipes);
        db.Currencies.AddRange(currencies.Currencies);
        db.Rarities.AddRange(rarities.Rarities);
        db.Npcs.AddRange(npcs.Npcs);
        db.NpcRelationTypes.AddRange(npcs.RelationTypes);
        db.Factions.AddRange(factions.Factions);
        db.DiplomaticRelations.AddRange(diplomacy.Relations);
        db.Maps.AddRange(maps.Maps);
        db.Dialogues.AddRange(dialogs.Dialogues);
        db.StoryEntries.AddRange(story.Entries);
        db.Quests.AddRange(quests.Quests);
        db.GameEvents.AddRange(events.Events);
        db.PlayerCharacters.AddRange(player.PlayerCharacters);
        db.SkillTrees.AddRange(player.SkillTrees);
        db.Skills.AddRange(player.Skills);
        db.CharacterClasses.AddRange(classes.Classes);
        db.GameEffects.AddRange(effects.Effects);
        db.Achievements.AddRange(achievements.Achievements);
        db.Collectibles.AddRange(collectibles.Collectibles);
        db.SoundEffects.AddRange(audio.SoundEffects);
        db.Cutscenes.AddRange(cutscenes.Cutscenes);
        db.LootTables.AddRange(loot.LootTables);
        db.WorldStates.AddRange(world.WorldStates);
        db.ContentTags.AddRange(tags.Tags);
        db.ContentLanguages.AddRange(localization.Languages);
        db.ContentTranslations.AddRange(localization.Translations);
        db.EnginePresets.AddRange(enginePresets.Presets);
        db.ExportProfiles.AddRange(exportProfiles.Profiles);
        db.ContentTypes.AddRange(typesAndFields.ContentTypes);
        db.FieldDefinitions.AddRange(typesAndFields.IndividualFields);
        // Geerbte Werte stehen im Archiv, damit die Engine die Vererbungskette nicht selbst
        // auflösen muss — als Zeile angelegt hätten sie die Vererbung aber materialisiert und
        // damit aufgelöst: Die Variante folgte ihrem Vorbild ab dem Umzug nicht mehr. Sie
        // entstehen beim Lesen ohnehin neu.
        db.FieldValues.AddRange(fieldValues.Values.Where(value => !value.IsInherited));
        db.ConditionSets.AddRange(conditions.ConditionSets);
        db.AssetTags.AddRange(assetsFile.AssetTags);
        db.Assets.AddRange(assetsFile.Assets);

        // Der Export ist die Sicherung des Projekts — sein Name und seine Beschreibung
        // gehören zum Stand und ziehen mit um.
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            project.Name = projectName;
            project.Description = projectDescription;
        }

        await db.SaveChangesAsync(ct);

        // Der eine Eintrag über den Vorgang, noch innerhalb der Transaktion: Wird der Import
        // zurückgerollt, verschwindet auch die Meldung darüber.
        await ChangeLog.RecordProjectActionAsync(
            db, projectId, project.Name, ChangeAction.Imported,
            messages["ChangeLog_ProjectImported", project.Name].Value, ct);

        await transaction.CommitAsync(ct);

        // Erst nach dem Commit: Dateien des ersetzten Bestands entfernen, die der neue Stand
        // nicht wiederverwendet. Vorher wäre bei einem Rollback der alte Stand ohne Dateien.
        foreach (var storageKey in replacedAssetKeys.Except(importedAssetKeys, StringComparer.Ordinal))
        {
            storage.Delete(storageKey);
        }

        var counts = new Dictionary<string, int>
        {
            [ModuleKeys.Items] = items.Items.Count,
            [ModuleKeys.Crafting] = crafting.Recipes.Count,
            [ModuleKeys.Currencies] = currencies.Currencies.Count,
            [ModuleKeys.Rarities] = rarities.Rarities.Count,
            [ModuleKeys.Npcs] = npcs.Npcs.Count,
            [ModuleKeys.Factions] = factions.Factions.Count,
            [ModuleKeys.Diplomacy] = diplomacy.Relations.Count,
            [ModuleKeys.Maps] = maps.Maps.Count,
            [ModuleKeys.Dialogs] = dialogs.Dialogues.Count,
            [ModuleKeys.Story] = story.Entries.Count,
            [ModuleKeys.Quests] = quests.Quests.Count,
            [ModuleKeys.Events] = events.Events.Count,
            [ModuleKeys.Player] = player.PlayerCharacters.Count + player.SkillTrees.Count + player.Skills.Count,
            [ModuleKeys.Classes] = classes.Classes.Count,
            [ModuleKeys.Loot] = loot.LootTables.Count,
            [ModuleKeys.World] = world.WorldStates.Count,
            [ModuleKeys.Effects] = effects.Effects.Count,
            [ModuleKeys.Achievements] = achievements.Achievements.Count,
            [ModuleKeys.Collectibles] = collectibles.Collectibles.Count,
            [ModuleKeys.Audio] = audio.SoundEffects.Count,
            [ModuleKeys.Cutscenes] = cutscenes.Cutscenes.Count,
            [ModuleKeys.Tags] = tags.Tags.Count,
            [ModuleKeys.Assets] = assetsFile.Assets.Count
        };

        return new ImportResult(counts, warnings);
    }

    /// <summary>
    /// Hat das Projekt schon irgendeinen Inhalt, den ein Import überschreiben würde?
    /// Intern statt privat, weil das Löschen eines Projekts (<see cref="ProjectService"/>)
    /// dieselbe Frage stellt, bevor es sein Sicherheitsnetz anlegt.
    /// </summary>
    internal static async Task<bool> HasContentAsync(GameDevManagerDbContext db, Guid projectId, CancellationToken ct) =>
        await db.Items.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Recipes.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Currencies.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Rarities.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Npcs.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.NpcRelationTypes.AnyAsync(t => t.GameProjectId == projectId, ct)
        || await db.Factions.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.DiplomaticRelations.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Maps.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Dialogues.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.StoryEntries.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Quests.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.GameEvents.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.PlayerCharacters.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.SkillTrees.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Skills.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.CharacterClasses.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.GameEffects.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Achievements.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Collectibles.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.SoundEffects.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Cutscenes.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.LootTables.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.ContentTypes.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.ContentTags.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.AssetTags.AnyAsync(e => e.GameProjectId == projectId, ct)
        || await db.Assets.AnyAsync(e => e.GameProjectId == projectId, ct);

    /// <summary>
    /// Entfernt den kompletten Bestand des Projekts, liefert die StorageKeys der entfernten
    /// Assets zurück. Läuft innerhalb der Import-Transaktion. Reihenfolge mit Bedacht:
    /// Feldwerte und Bedingungen hängen ohne Fremdschlüssel an ihren Besitzern, die Arten
    /// dürfen wegen des Restrict-Fremdschlüssels erst nach den Entitäten fallen.
    /// Die Moduleinstellungen (Module an/aus) bleiben absichtlich stehen — sie sind
    /// Werkzeug-Konfiguration, kein Inhalt, und stehen auch nicht im Export.
    /// Intern statt privat, weil das Löschen eines Projekts (<see cref="ProjectService"/>)
    /// denselben Wipe braucht.
    /// </summary>
    internal static async Task<List<string>> WipeProjectAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        // Feldwerte und individuelle Felder tragen keine Projekt-Spalte — sie werden über die
        // Menge aller Entitäts-GUIDs des Projekts gefunden, wie beim Export.
        var entityIds = new List<Guid>();

        async Task CollectAsync<T>(IQueryable<T> set) where T : ContentEntity =>
            entityIds.AddRange(await set
                .Where(e => e.GameProjectId == projectId)
                .Select(e => e.Id)
                .ToListAsync(ct));

        await CollectAsync(db.Items);
        await CollectAsync(db.Recipes);
        await CollectAsync(db.Currencies);
        await CollectAsync(db.Rarities);
        await CollectAsync(db.Npcs);
        await CollectAsync(db.Factions);
        await CollectAsync(db.DiplomaticRelations);
        await CollectAsync(db.Maps);
        await CollectAsync(db.Dialogues);
        await CollectAsync(db.StoryEntries);
        await CollectAsync(db.Quests);
        await CollectAsync(db.GameEvents);
        await CollectAsync(db.Skills);
        await CollectAsync(db.CharacterClasses);
        await CollectAsync(db.GameEffects);
        await CollectAsync(db.Achievements);
        await CollectAsync(db.Collectibles);
        await CollectAsync(db.SoundEffects);
        await CollectAsync(db.Cutscenes);
        await CollectAsync(db.LootTables);
        await CollectAsync(db.WorldStates);

        entityIds.AddRange(await db.PlayerCharacters
            .Where(p => p.GameProjectId == projectId).Select(p => p.Id).ToListAsync(ct));
        entityIds.AddRange(await db.SkillTrees
            .Where(t => t.GameProjectId == projectId).Select(t => t.Id).ToListAsync(ct));

        await db.FieldValues
            .Where(v => entityIds.Contains(v.OwnerEntityId))
            .ExecuteDeleteAsync(ct);
        await db.FieldDefinitions
            .Where(f => f.OwnerEntityId != null && entityIds.Contains(f.OwnerEntityId.Value))
            .ExecuteDeleteAsync(ct);

        // Bedingungssätze tragen die Projekt-GUID — das deckt auch die Sätze an Teilobjekten
        // (Händler-Posten, Dialogzeilen) ab, deren Besitzer-GUIDs oben nicht eingesammelt sind.
        await db.ConditionSets
            .Where(s => s.GameProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        // Tags samt Geltungsbereichen und Zuweisungen (Kaskade), Asset-Stichwörter ebenso.
        await db.ContentTags.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.AssetTags.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        // Übersetzungen vor den Sprachen: Sie hängen über das Kürzel daran, nicht über einen
        // Fremdschlüssel — sonst blieben sie als Waisen stehen.
        await db.ContentTranslations.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.ContentLanguages.Where(l => l.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        // Presets samt Zuordnungen (Kaskade) — sie hängen am Projekt, nicht am Inhalt.
        await db.EnginePresets.Where(p => p.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.ExportProfiles.Where(p => p.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        var assetKeys = await db.Assets
            .Where(a => a.GameProjectId == projectId)
            .Select(a => a.StorageKey)
            .ToListAsync(ct);
        await db.Assets.Where(a => a.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        // Die Modultabellen; Kind-Sammlungen (Zutaten, Posten, Zeilen, …) fallen per Kaskade.
        await db.Items.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Recipes.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Currencies.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Rarities.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Npcs.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        // Erst nach den NPCs — der Restrict-Fremdschlüssel der Beziehungen blockierte sonst.
        await db.NpcRelationTypes.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Factions.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.DiplomaticRelations.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Maps.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Dialogues.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.StoryEntries.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Quests.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.GameEvents.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Skills.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.CharacterClasses.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.GameEffects.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Achievements.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Collectibles.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.SoundEffects.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.Cutscenes.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.LootTables.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.WorldStates.Where(e => e.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.PlayerCharacters.Where(p => p.GameProjectId == projectId).ExecuteDeleteAsync(ct);
        await db.SkillTrees.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        // Zuletzt die Arten (Felder und deren Werte fallen per Kaskade) — vorher blockierte
        // der Restrict-Fremdschlüssel der Entitäten.
        await db.ContentTypes.Where(t => t.GameProjectId == projectId).ExecuteDeleteAsync(ct);

        return assetKeys;
    }

    // ------------------------------------------------------------------ Datei-Wrapper
    // Eine Klasse je Inhaltsdatei, Eigenschaftsnamen wie die anonymen Objekte im
    // ExportService — beide Seiten müssen synchron bleiben (FormatVersion!).

    private sealed class ItemsFile { public List<Item> Items { get; set; } = []; }

    private sealed class CraftingFile { public List<Recipe> Recipes { get; set; } = []; }

    private sealed class CurrenciesFile { public List<Currency> Currencies { get; set; } = []; }

    private sealed class RaritiesFile { public List<Rarity> Rarities { get; set; } = []; }

    private sealed class NpcsFile
    {
        public List<Npc> Npcs { get; set; } = [];

        public List<NpcRelationType> RelationTypes { get; set; } = [];
    }

    private sealed class FactionsFile { public List<Faction> Factions { get; set; } = []; }

    private sealed class DiplomacyFile { public List<DiplomaticRelation> Relations { get; set; } = []; }

    private sealed class MapsFile { public List<GameMap> Maps { get; set; } = []; }

    private sealed class DialogsFile { public List<Dialogue> Dialogues { get; set; } = []; }

    private sealed class StoryFile { public List<StoryEntry> Entries { get; set; } = []; }

    private sealed class QuestsFile { public List<Quest> Quests { get; set; } = []; }

    private sealed class EventsFile { public List<GameEvent> Events { get; set; } = []; }

    private sealed class PlayerFile
    {
        public List<PlayerCharacter> PlayerCharacters { get; set; } = [];

        public List<SkillTree> SkillTrees { get; set; } = [];

        public List<Skill> Skills { get; set; } = [];
    }

    private sealed class ClassesFile { public List<CharacterClass> Classes { get; set; } = []; }

    private sealed class LootFile { public List<LootTable> LootTables { get; set; } = []; }

    private sealed class WorldFile { public List<WorldState> WorldStates { get; set; } = []; }

    private sealed class EffectsFile { public List<GameEffect> Effects { get; set; } = []; }

    private sealed class AchievementsFile { public List<Achievement> Achievements { get; set; } = []; }

    private sealed class CollectiblesFile { public List<Collectible> Collectibles { get; set; } = []; }

    private sealed class AudioFile { public List<SoundEffect> SoundEffects { get; set; } = []; }

    private sealed class CutscenesFile { public List<Cutscene> Cutscenes { get; set; } = []; }

    private sealed class TagsFile { public List<ContentTag> Tags { get; set; } = []; }

    private sealed class EnginePresetsFile { public List<EnginePreset> Presets { get; set; } = []; }

    private sealed class ExportProfilesFile { public List<ExportProfile> Profiles { get; set; } = []; }

    private sealed class LocalizationFile
    {
        public List<ContentLanguage> Languages { get; set; } = [];

        public List<ContentTranslation> Translations { get; set; } = [];
    }

    private sealed class TypesAndFieldsFile
    {
        public List<ContentType> ContentTypes { get; set; } = [];

        public List<FieldDefinition> IndividualFields { get; set; } = [];
    }

    private sealed class FieldValuesFile { public List<FieldValue> Values { get; set; } = []; }

    private sealed class ConditionsFile { public List<ConditionSet> ConditionSets { get; set; } = []; }

    private sealed class AssetsFile
    {
        public List<AssetTag> AssetTags { get; set; } = [];

        public List<Asset> Assets { get; set; } = [];
    }
}
