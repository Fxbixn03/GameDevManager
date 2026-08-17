using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Vertonung der Dialogzeilen (F10). Kein eigener Datenbestand: Eine Aufnahme ist ein
/// Asset an der GUID der Zeile — dieselbe Anbindung wie beim Skizzenbild einer
/// Cutscene-Einstellung; hinzu kamen nur Sprache und Sprecher am Asset.
/// </summary>
public class VoiceOverTests
{
    private static async Task<Dialogue> SeedDialogueAsync(TestDatabase test, int lineCount = 2)
    {
        await using var db = test.CreateContext();

        var dialogue = new Dialogue
        {
            GameProjectId = test.ProjectId,
            Name = "Am Tor",
            Kind = DialogueKind.Conversation,
            IncludesPlayer = true
        };

        for (var index = 0; index < lineCount; index++)
        {
            dialogue.Lines.Add(new DialogueLine
            {
                DialogueId = dialogue.Id,
                Text = $"Zeile {index + 1}",
                SortOrder = index
            });
        }

        db.Dialogues.Add(dialogue);
        await db.SaveChangesAsync();

        return dialogue;
    }

    private static async Task SeedLanguagesAsync(TestDatabase test)
    {
        var localization = test.GetService<LocalizationService>();

        await localization.SaveLanguageAsync(test.ProjectId, new ContentLanguage { Code = "de", Name = "Deutsch" });
        await localization.SaveLanguageAsync(test.ProjectId, new ContentLanguage { Code = "en", Name = "Englisch" });
    }

    private static Task<Asset> UploadRecordingAsync(TestDatabase test, string fileName = "zeile1-de.ogg") =>
        test.GetService<AssetService>().UploadAsync(
            test.ProjectId, fileName, "audio/ogg", new MemoryStream([1, 2, 3, 4]));

    [Fact]
    public async Task Die_Uebersicht_zeigt_je_Zeile_eine_Zelle_je_Sprache()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test);

        var overview = await test.GetService<VoiceOverService>()
            .GetOverviewAsync(test.ProjectId, dialogue.Id);

        Assert.NotNull(overview);
        Assert.Equal(2, overview.Lines.Count);

        // Aufgenommen wird in allen Sprachen, die Ausgangssprache eingeschlossen: Ihr Text
        // steht am Inhalt, ihre Aufnahme aber nirgends.
        Assert.All(overview.Lines, line => Assert.Equal(2, line.Takes.Count));
        Assert.All(overview.Lines, line => Assert.All(line.Takes, take => Assert.False(take.IsRecorded)));
    }

    [Fact]
    public async Task Eine_Aufnahme_haengt_an_der_Zeile_und_traegt_Sprache_und_Sprecher()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test);
        var line = dialogue.Lines[0];

        var voiceOvers = test.GetService<VoiceOverService>();
        var asset = await UploadRecordingAsync(test);

        await voiceOvers.SetRecordingAsync(asset.Id, line.Id, "de", "Anja Berg");

        var overview = await voiceOvers.GetOverviewAsync(test.ProjectId, dialogue.Id);
        var take = overview!.Lines.Single(l => l.LineId == line.Id).Takes.Single(t => t.LanguageCode == "de");

        Assert.True(take.IsRecorded);
        Assert.Equal("Anja Berg", take.VoiceActor);

        // Die andere Sprache bleibt offen — eine Aufnahme gilt genau für eine.
        Assert.False(overview.Lines.Single(l => l.LineId == line.Id).Takes.Single(t => t.LanguageCode == "en").IsRecorded);
    }

    [Fact]
    public async Task Eine_zweite_Aufnahme_derselben_Sprache_loest_die_erste_ab()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test);
        var line = dialogue.Lines[0];

        var voiceOvers = test.GetService<VoiceOverService>();

        var first = await UploadRecordingAsync(test, "alt.ogg");
        await voiceOvers.SetRecordingAsync(first.Id, line.Id, "de", null);

        var second = await UploadRecordingAsync(test, "neu.ogg");
        await voiceOvers.SetRecordingAsync(second.Id, line.Id, "de", null);

        var overview = await voiceOvers.GetOverviewAsync(test.ProjectId, dialogue.Id);
        var take = overview!.Lines.Single(l => l.LineId == line.Id).Takes.Single(t => t.LanguageCode == "de");

        Assert.Equal("neu.ogg", take.FileName);

        // Die abgelöste Aufnahme wird wirklich entfernt, samt ihrer Datei — sonst wüchse der
        // Speicher mit jedem neuen Take.
        await using var db = test.CreateContext();
        Assert.Null(await db.Assets.FirstOrDefaultAsync(a => a.Id == first.Id));
        Assert.Empty(await test.GetService<AssetService>().FindOrphanedFilesAsync());
    }

    [Fact]
    public async Task Eine_unbekannte_Sprache_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test);
        var asset = await UploadRecordingAsync(test);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<VoiceOverService>()
                .SetRecordingAsync(asset.Id, dialogue.Lines[0].Id, "fr", null));
    }

    [Fact]
    public async Task Der_Fortschritt_zaehlt_je_Sprache()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test, lineCount: 4);

        var voiceOvers = test.GetService<VoiceOverService>();

        foreach (var line in dialogue.Lines.Take(3))
        {
            var asset = await UploadRecordingAsync(test, $"{line.Id:N}.ogg");
            await voiceOvers.SetRecordingAsync(asset.Id, line.Id, "de", null);
        }

        var overview = await voiceOvers.GetOverviewAsync(test.ProjectId, dialogue.Id);
        var german = overview!.Progress.Single(p => p.LanguageCode == "de");

        Assert.Equal(4, german.Total);
        Assert.Equal(3, german.Recorded);
        Assert.Equal(1, german.Missing);
        Assert.Equal(75, german.Percent);
    }

    [Fact]
    public async Task Der_Health_Check_meldet_nur_Sprachen_in_denen_der_Text_vorliegt()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test, lineCount: 1);
        var line = dialogue.Lines[0];

        var voiceOvers = test.GetService<VoiceOverService>();

        // Nur die Ausgangssprache hat Text — die englische Fassung gibt es noch nicht.
        var gaps = await voiceOvers.FindMissingRecordingsAsync(test.ProjectId);
        Assert.Equal("de", Assert.Single(gaps).LanguageCode);

        // Sobald die Zeile übersetzt ist, fehlt auch die englische Aufnahme.
        await test.GetService<LocalizationService>().SaveAsync(
            test.ProjectId, line.Id, ModuleKeys.Dialogs, TranslationSlots.Text, "en", "Line 1", "Zeile 1");

        gaps = await voiceOvers.FindMissingRecordingsAsync(test.ProjectId);
        Assert.Equal(2, gaps.Count);

        // Und eine vorhandene Aufnahme fällt aus dem Fund heraus.
        var asset = await UploadRecordingAsync(test);
        await voiceOvers.SetRecordingAsync(asset.Id, line.Id, "de", null);

        gaps = await voiceOvers.FindMissingRecordingsAsync(test.ProjectId);
        Assert.Equal("en", Assert.Single(gaps).LanguageCode);
    }

    [Fact]
    public async Task Ohne_Sprachen_findet_der_Health_Check_nichts()
    {
        using var test = new TestDatabase();
        await SeedDialogueAsync(test, lineCount: 3);

        // Wer keine Lokalisierung pflegt, plant auch keine Vertonung — eine Meldung an jedem
        // Dialog wäre Rauschen.
        Assert.Empty(await test.GetService<VoiceOverService>().FindMissingRecordingsAsync(test.ProjectId));
    }

    [Fact]
    public async Task Beim_Loeschen_des_Dialogs_gehen_die_Aufnahmen_mit()
    {
        using var test = new TestDatabase();
        await SeedLanguagesAsync(test);
        var dialogue = await SeedDialogueAsync(test);

        var voiceOvers = test.GetService<VoiceOverService>();
        var asset = await UploadRecordingAsync(test);
        await voiceOvers.SetRecordingAsync(asset.Id, dialogue.Lines[0].Id, "de", null);

        await test.GetService<DialogueService>().DeleteDialogueAsync(dialogue.Id);

        await using var db = test.CreateContext();
        Assert.Empty(await db.Assets.ToListAsync());

        // Auch die Datei — sonst bliebe sie als Waise im Speicher liegen.
        Assert.Empty(await test.GetService<AssetService>().FindOrphanedFilesAsync());
    }
}
