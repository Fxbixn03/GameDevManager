using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Lokalisierung der Spielinhalte: Sprachen, Übersetzungen und der Fortschritt. Der
/// interessante Teil ist nicht das Speichern, sondern die Frage „was ist noch offen?“ — dazu
/// gehört auch das, was <b>veraltet</b> ist, weil sich das Original geändert hat.
/// </summary>
public class LocalizationTests
{
    private static async Task<Guid> SeedItemAsync(TestDatabase database, string name, string? description = null)
    {
        await using var db = database.CreateContext();

        var item = new Item
        {
            GameProjectId = database.ProjectId,
            Name = name,
            Description = description
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    private static async Task<(string Source, string Target)> SeedLanguagesAsync(TestDatabase database)
    {
        var service = database.GetService<LocalizationService>();

        await service.SaveLanguageAsync(database.ProjectId, new ContentLanguage { Code = "de", Name = "Deutsch" });
        await service.SaveLanguageAsync(database.ProjectId, new ContentLanguage { Code = "en", Name = "Englisch" });

        return ("de", "en");
    }

    [Fact]
    public async Task Die_erste_Sprache_wird_zur_Ausgangssprache()
    {
        using var database = new TestDatabase();
        var (source, target) = await SeedLanguagesAsync(database);

        var languages = await database.GetService<LocalizationService>().GetLanguagesAsync(database.ProjectId);

        Assert.True(languages.Single(l => l.Code == source).IsSource);
        Assert.False(languages.Single(l => l.Code == target).IsSource);
    }

    [Fact]
    public async Task Es_gibt_immer_nur_eine_Ausgangssprache()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        var service = database.GetService<LocalizationService>();
        var english = (await service.GetLanguagesAsync(database.ProjectId)).Single(l => l.Code == "en");

        english.IsSource = true;
        await service.SaveLanguageAsync(database.ProjectId, english);

        var languages = await service.GetLanguagesAsync(database.ProjectId);

        Assert.Single(languages, language => language.IsSource);
        Assert.True(languages.Single(l => l.Code == "en").IsSource);
    }

    [Fact]
    public async Task Die_Ausgangssprache_laesst_sich_nicht_loeschen()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        var service = database.GetService<LocalizationService>();
        var german = (await service.GetLanguagesAsync(database.ProjectId)).Single(l => l.Code == "de");

        await Assert.ThrowsAsync<ContentValidationException>(() => service.DeleteLanguageAsync(german.Id));
    }

    [Fact]
    public async Task Ein_doppeltes_Kuerzel_wird_abgelehnt()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<LocalizationService>().SaveLanguageAsync(
                database.ProjectId, new ContentLanguage { Code = "en", Name = "English" }));
    }

    [Fact]
    public async Task Die_Arbeitsliste_zeigt_Name_Beschreibung_und_Textfelder()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert", "Scharf.");

        await using (var db = database.CreateContext())
        {
            var type = new ContentType
            {
                GameProjectId = database.ProjectId,
                ModuleKey = ModuleKeys.Items,
                Name = "Waffe"
            };
            var lore = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Legende",
                Type = ContentFieldType.MultilineText
            };
            // Eine Zahl ist in jeder Sprache dieselbe — sie darf nicht in der Liste stehen.
            var damage = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Schaden",
                Type = ContentFieldType.Integer
            };

            var item = await db.Items.FirstAsync(i => i.Id == itemId);
            item.ContentTypeId = type.Id;

            db.ContentTypes.Add(type);
            db.FieldDefinitions.AddRange(lore, damage);
            db.FieldValues.AddRange(
                new FieldValue
                {
                    OwnerEntityId = itemId,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = lore.Id,
                    TextValue = "Geschmiedet im Berg."
                },
                new FieldValue
                {
                    OwnerEntityId = itemId,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = damage.Id,
                    NumberValue = 12
                });

            await db.SaveChangesAsync();
        }

        var rows = await database.GetService<LocalizationService>()
            .GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en");

        Assert.Equal(
            new[] { "Schwert", "Scharf.", "Geschmiedet im Berg." },
            rows.Select(row => row.SourceText));
        Assert.All(rows, row => Assert.True(row.IsMissing));
    }

    [Fact]
    public async Task Eine_gespeicherte_Uebersetzung_gilt_und_ein_leerer_Text_loescht_sie()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert");

        var service = database.GetService<LocalizationService>();

        await service.SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        var rows = await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en");
        Assert.Equal("Sword", Assert.Single(rows).Text);

        await service.SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "  ", "Schwert");

        await using var db = database.CreateContext();
        Assert.Empty(await db.ContentTranslations.ToListAsync());
    }

    [Fact]
    public async Task Aendert_sich_das_Original_gilt_die_Uebersetzung_als_veraltet()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert");

        var service = database.GetService<LocalizationService>();
        await service.SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        Assert.False(Assert.Single(await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en")).IsStale);

        // Das Original wandert weiter — die Übersetzung bleibt stehen und ist damit falsch.
        await using (var db = database.CreateContext())
        {
            var item = await db.Items.FirstAsync(i => i.Id == itemId);
            item.Name = "Langschwert";
            await db.SaveChangesAsync();
        }

        var row = Assert.Single(await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en"));

        Assert.True(row.IsStale);
        Assert.False(row.IsMissing);
        Assert.Equal("Schwert", row.TranslatedFrom);
    }

    [Fact]
    public async Task Der_Fortschritt_zaehlt_Uebersetztes_Offenes_und_Veraltetes()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        var first = await SeedItemAsync(database, "Schwert");
        await SeedItemAsync(database, "Axt");

        var service = database.GetService<LocalizationService>();
        await service.SaveAsync(
            database.ProjectId, first, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        var progress = Assert.Single(await service.GetProgressAsync(database.ProjectId));

        Assert.Equal("en", progress.LanguageCode);
        Assert.Equal(2, progress.Total);
        Assert.Equal(1, progress.Translated);
        Assert.Equal(1, progress.Missing);
        Assert.Equal(0, progress.Stale);
        Assert.Equal(50, progress.Percent);
    }

    [Fact]
    public async Task Beim_Loeschen_einer_Entitaet_gehen_ihre_Uebersetzungen_mit()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert");

        await database.GetService<LocalizationService>().SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        await database.GetService<ItemService>().DeleteItemAsync(itemId);

        await using var db = database.CreateContext();
        Assert.Empty(await db.ContentTranslations.ToListAsync());
    }

    [Fact]
    public async Task Die_Arbeitsliste_zeigt_Dialogzeilen_und_Antwortmoeglichkeiten()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        Guid lineId;
        Guid choiceId;

        await using (var db = database.CreateContext())
        {
            var dialogue = new Dialogue
            {
                GameProjectId = database.ProjectId,
                Name = "Torwache",
                Kind = DialogueKind.Conversation,
                IncludesPlayer = true
            };

            var line = new DialogueLine { DialogueId = dialogue.Id, Text = "Halt!", SortOrder = 0 };
            var choice = new DialogueChoice { DialogueLineId = line.Id, Text = "Wer bist du?" };

            line.Choices.Add(choice);
            dialogue.Lines.Add(line);

            db.Dialogues.Add(dialogue);
            await db.SaveChangesAsync();

            lineId = line.Id;
            choiceId = choice.Id;
        }

        var rows = await database.GetService<LocalizationService>()
            .GetRowsAsync(database.ProjectId, ModuleKeys.Dialogs, "en");

        // Die Zeile hängt an ihrer eigenen GUID, nicht an der des Dialogs.
        var lineRow = Assert.Single(rows, row => row.OwnerEntityId == lineId);
        Assert.Equal("Halt!", lineRow.SourceText);
        Assert.Equal(TranslationSlots.Text, lineRow.Slot);
        Assert.Equal("Torwache", lineRow.OwnerName);

        var choiceRow = Assert.Single(rows, row => row.OwnerEntityId == choiceId);
        Assert.Equal("Wer bist du?", choiceRow.SourceText);

        // Name des Dialogs, dann seine Zeile, dann die Antwort daran — beieinander.
        Assert.Equal(
            new[] { "Torwache", "Halt!", "Wer bist du?" },
            rows.Select(row => row.SourceText));
    }

    [Fact]
    public async Task Die_Arbeitsliste_zeigt_den_Story_Text()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        Guid entryId;

        await using (var db = database.CreateContext())
        {
            var entry = new StoryEntry
            {
                GameProjectId = database.ProjectId,
                Name = "Der Aufbruch",
                Body = "Sie zogen nach Norden."
            };

            db.StoryEntries.Add(entry);
            await db.SaveChangesAsync();

            entryId = entry.Id;
        }

        var rows = await database.GetService<LocalizationService>()
            .GetRowsAsync(database.ProjectId, ModuleKeys.Story, "en");

        var body = Assert.Single(rows, row => row.Slot == TranslationSlots.Body);

        Assert.Equal(entryId, body.OwnerEntityId);
        Assert.Equal("Sie zogen nach Norden.", body.SourceText);
    }

    [Fact]
    public async Task Uebersetzte_Dialogzeilen_zaehlen_im_Fortschritt_und_gehen_beim_Loeschen_mit()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        Guid dialogueId;
        Guid lineId;

        await using (var db = database.CreateContext())
        {
            var dialogue = new Dialogue
            {
                GameProjectId = database.ProjectId,
                Name = "Torwache",
                Kind = DialogueKind.Conversation
            };

            var line = new DialogueLine { DialogueId = dialogue.Id, Text = "Halt!", SortOrder = 0 };
            dialogue.Lines.Add(line);

            db.Dialogues.Add(dialogue);
            await db.SaveChangesAsync();

            dialogueId = dialogue.Id;
            lineId = line.Id;
        }

        var service = database.GetService<LocalizationService>();

        // Zwei offene Texte: der Name des Dialogs und die Zeile darin.
        Assert.Equal(2, Assert.Single(await service.GetProgressAsync(database.ProjectId)).Total);

        await service.SaveAsync(
            database.ProjectId, lineId, ModuleKeys.Dialogs, TranslationSlots.Text, "en", "Halt!", "Halt!");

        Assert.Equal(1, Assert.Single(await service.GetProgressAsync(database.ProjectId)).Translated);

        // Die Zeile ist ein Teilobjekt — ihre Übersetzung hängt an einer GUID, die es nach
        // dem Löschen des Dialogs nicht mehr gibt.
        await database.GetService<DialogueService>().DeleteDialogueAsync(dialogueId);

        await using var check = database.CreateContext();
        Assert.Empty(await check.ContentTranslations.ToListAsync());
    }

    [Fact]
    public async Task Die_Tabelle_traegt_die_offenen_Texte_und_kommt_ausgefuellt_zurueck()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);

        var swordId = await SeedItemAsync(database, "Schwert");
        await SeedItemAsync(database, "Axt");

        var service = database.GetService<LocalizationService>();
        await service.SaveAsync(
            database.ProjectId, swordId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        // „Nur Offenes“ lässt das bereits Übersetzte weg — sonst wäre die Datei unbrauchbar.
        var open = await service.ExportCsvAsync(database.ProjectId, "en", openOnly: true);
        Assert.DoesNotContain("Schwert", open, StringComparison.Ordinal);
        Assert.Contains("Axt", open, StringComparison.Ordinal);

        Assert.Contains(
            "Schwert",
            await service.ExportCsvAsync(database.ProjectId, "en", openOnly: false),
            StringComparison.Ordinal);

        // Ausgefüllt zurück: Die Zeile findet ihr Ziel über id + slot.
        var axeId = (await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en"))
            .Single(row => row.SourceText == "Axt").OwnerEntityId;

        var filled = string.Join(
            "\n",
            "id;slot;modul;entität;ausgangstext;übersetzung;stand",
            $"{axeId};{TranslationSlots.Name};{ModuleKeys.Items};Axt;Axt;Axe;fehlt");

        var result = await service.ImportCsvAsync(database.ProjectId, "en", filled);

        Assert.Equal(1, result.Created);
        Assert.Empty(result.Warnings);
        Assert.Equal(
            "Axe",
            (await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en"))
                .Single(row => row.OwnerEntityId == axeId).Text);
    }

    [Fact]
    public async Task Eine_leere_Zelle_loescht_und_eine_kaputte_Zeile_ist_nur_eine_Warnung()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert");

        var service = database.GetService<LocalizationService>();
        await service.SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        var content = string.Join(
            "\n",
            "id;slot;modul;entität;ausgangstext;übersetzung;stand",
            $"{itemId};{TranslationSlots.Name};{ModuleKeys.Items};Schwert;Schwert;;übersetzt",
            $"{Guid.NewGuid()};{TranslationSlots.Name};{ModuleKeys.Items};Weg;Weg;Gone;fehlt",
            "kaputt;;;;;;");

        var result = await service.ImportCsvAsync(database.ProjectId, "en", content);

        // Der leere Text löscht die Übersetzung — „nicht übersetzt“ soll nichts hinterlassen.
        await using var db = database.CreateContext();
        Assert.Empty(await db.ContentTranslations.ToListAsync());

        // Eine unbekannte und eine unlesbare Zeile werfen nicht, sie melden sich.
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public async Task Sprachen_und_Uebersetzungen_ueberstehen_Export_und_Import()
    {
        using var database = new TestDatabase();
        await SeedLanguagesAsync(database);
        var itemId = await SeedItemAsync(database, "Schwert");

        await database.GetService<LocalizationService>().SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        using var zip = new MemoryStream();
        await database.GetService<ExportService>()
            .WriteExportAsync(database.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await database.GetService<ImportService>().ImportAsync(database.ProjectId, zip, replaceExisting: true);

        var service = database.GetService<LocalizationService>();

        Assert.Equal(2, (await service.GetLanguagesAsync(database.ProjectId)).Count);
        Assert.Equal("Sword", Assert.Single(await service.GetRowsAsync(database.ProjectId, ModuleKeys.Items, "en")).Text);
    }
}
