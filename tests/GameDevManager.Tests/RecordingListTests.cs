using System.IO.Compression;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Aufnahmeliste fürs Tonstudio: das Skript folgt dem Health Check, der ZIP-Rückweg
/// ordnet über die Dateinamen-Vorgabe zu — und meldet Unklares statt zu raten.
/// </summary>
public class RecordingListTests
{
    /// <summary>Ein Dialog mit zwei Zeilen, eine Sprache — die zweite Zeile spricht ein NPC.</summary>
    private static async Task<(Guid DialogueId, Guid FirstLineId, Guid SecondLineId)> SeedAsync(
        TestDatabase test, bool withTargetLanguage = false)
    {
        await using var db = test.CreateContext();

        db.ContentLanguages.Add(new ContentLanguage
        {
            GameProjectId = test.ProjectId, Code = "de", Name = "Deutsch", IsSource = true
        });

        if (withTargetLanguage)
        {
            db.ContentLanguages.Add(new ContentLanguage
            {
                GameProjectId = test.ProjectId, Code = "en", Name = "English"
            });
        }

        var alrik = new Npc { GameProjectId = test.ProjectId, Name = "Alrik" };

        var dialogue = new Dialogue
        {
            GameProjectId = test.ProjectId,
            Name = "Begrüßung",
            Kind = DialogueKind.Conversation
        };

        var first = new DialogueLine
        {
            DialogueId = dialogue.Id, Text = "Seid gegrüßt!", SortOrder = 0, SpeakerNpcId = alrik.Id
        };
        var second = new DialogueLine
        {
            DialogueId = dialogue.Id, Text = "Was führt Euch her?", SortOrder = 1, SpeakerNpcId = alrik.Id
        };

        db.Npcs.Add(alrik);
        db.Dialogues.Add(dialogue);
        db.DialogueLines.AddRange(first, second);
        await db.SaveChangesAsync();

        return (dialogue.Id, first.Id, second.Id);
    }

    [Fact]
    public async Task Das_Skript_traegt_Sprecher_Kontext_und_die_Dateinamen_Vorgabe()
    {
        using var test = new TestDatabase();
        var (_, firstId, secondId) = await SeedAsync(test);

        var script = await test.GetService<RecordingListService>().GetScriptAsync(test.ProjectId, "de");

        Assert.Equal(2, script.Count);

        var first = script.Single(line => line.LineId == firstId);
        Assert.Equal("Alrik", first.Speaker);
        Assert.Null(first.PreviousText);
        Assert.Equal($"{firstId:N}.de.wav", first.FileName);

        // Die zweite Zeile trägt die erste als Kontext — ein Sprecher braucht den Anschluss.
        var second = script.Single(line => line.LineId == secondId);
        Assert.Equal("Seid gegrüßt!", second.PreviousText);

        // Das CSV führt dieselben Zeilen, mit Kopfzeile.
        var csv = RecordingListService.BuildCsv(script);
        Assert.Contains("datei;dialog;sprecher;text;kontext", csv);
        Assert.Contains("Seid gegrüßt!", csv);
    }

    [Fact]
    public async Task In_der_Zielsprache_spricht_das_Studio_die_Uebersetzung()
    {
        using var test = new TestDatabase();
        var (_, firstId, _) = await SeedAsync(test, withTargetLanguage: true);

        await using (var db = test.CreateContext())
        {
            db.ContentTranslations.Add(new ContentTranslation
            {
                GameProjectId = test.ProjectId,
                OwnerEntityId = firstId,
                OwnerModuleKey = ModuleKeys.Dialogs,
                Slot = TranslationSlots.Text,
                LanguageCode = "en",
                Text = "Greetings!",
                SourceText = "Seid gegrüßt!"
            });
            await db.SaveChangesAsync();
        }

        // Offen in „en“ ist nur die übersetzte Zeile — der Health Check bleibt die Quelle.
        var script = await test.GetService<RecordingListService>().GetScriptAsync(test.ProjectId, "en");

        var line = Assert.Single(script);
        Assert.Equal(firstId, line.LineId);
        Assert.Equal("Greetings!", line.Text);
    }

    [Fact]
    public async Task Das_ZIP_ordnet_ueber_den_Dateinamen_zu_und_meldet_Unklares()
    {
        using var test = new TestDatabase();
        var (_, firstId, _) = await SeedAsync(test);

        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            async Task AddAsync(string name)
            {
                var entry = archive.CreateEntry(name);
                await using var stream = entry.Open();
                await stream.WriteAsync(new byte[] { 1, 2, 3, 4 });
            }

            await AddAsync($"{firstId:N}.de.wav");            // passt
            await AddAsync("notizen.txt");                    // kein Muster
            await AddAsync($"{Guid.NewGuid():N}.de.wav");     // unbekannte Zeile
            await AddAsync($"{firstId:N}.fr.wav");            // unbekannte Sprache
        }

        zip.Position = 0;

        var result = await test.GetService<RecordingListService>().ImportZipAsync(test.ProjectId, zip);

        Assert.Equal(1, result.Assigned);
        Assert.Equal(3, result.Conflicts.Count);

        await using var db = test.CreateContext();

        var recording = Assert.Single(await db.Assets
            .Where(asset => asset.OwnerEntityId == firstId)
            .ToListAsync());
        Assert.Equal("de", recording.LanguageCode);

        // Die zugeordnete Zeile ist damit nicht mehr offen — dieselbe Wahrheit wie der Check.
        var script = await test.GetService<RecordingListService>().GetScriptAsync(test.ProjectId, "de");
        Assert.DoesNotContain(script, line => line.LineId == firstId);
    }

    [Fact]
    public async Task Eine_zweite_Aufnahme_derselben_Sprache_ersetzt_die_erste()
    {
        using var test = new TestDatabase();
        var (_, firstId, _) = await SeedAsync(test);

        var recordings = test.GetService<RecordingListService>();

        for (var round = 0; round < 2; round++)
        {
            using var zip = new MemoryStream();
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry($"{firstId:N}.de.wav");
                await using var stream = entry.Open();
                await stream.WriteAsync(new byte[] { 1, 2, 3 });
            }

            zip.Position = 0;
            await recordings.ImportZipAsync(test.ProjectId, zip);
        }

        await using var db = test.CreateContext();
        Assert.Single(await db.Assets.Where(asset => asset.OwnerEntityId == firstId).ToListAsync());
    }
}
