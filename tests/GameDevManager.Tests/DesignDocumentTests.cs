using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Das Design-Dokument: der Bestand als eigenständige HTML-Datei. Gesät wird über das
/// Beispielprojekt — genau der Bestand, den auch ein Nutzer als Erstes hineingibt.
/// </summary>
public class DesignDocumentTests
{
    [Fact]
    public async Task Das_Dokument_traegt_die_gewaehlten_Kapitel()
    {
        using var test = new TestDatabase();
        var project = await test.GetService<SampleProjectService>().CreateAsync();

        // Ein Story-Abschnitt mit Markdown und Erwähnung — der textlastigste Fall.
        var story = test.GetService<StoryService>();
        var entry = await story.LoadForEditAsync(project.Id, null);
        entry!.Entity.Name = "Der Aufbruch";
        entry.Entity.Body = "Er zog **das Schwert** — @Eisenschwert.";
        await story.SaveEntryAsync(entry);

        var html = await test.GetService<DesignDocumentService>()
            .BuildHtmlAsync(project.Id, DesignChapters.All);

        // Eigenständig und vollständig: Kopf, Kapitel, Inhalte.
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains(project.Name, html);
        Assert.Contains("Der Aufbruch", html);
        Assert.Contains("<b>das Schwert</b>", html);
        Assert.Contains("Eisenschwert", html);
        Assert.Contains("Alrik der Schmied", html);

        // Keine Verweise nach außen — die Datei muss allein lesbar sein.
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public async Task Abgewaehlte_Kapitel_fehlen()
    {
        using var test = new TestDatabase();
        var project = await test.GetService<SampleProjectService>().CreateAsync();

        var html = await test.GetService<DesignDocumentService>().BuildHtmlAsync(
            project.Id,
            new DesignChapters(Story: false, Factions: false, Npcs: true, Quests: false, Items: false));

        Assert.Contains("Alrik der Schmied", html);
        Assert.DoesNotContain("Eisenerz", html);
    }
}
