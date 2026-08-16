using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Erwähnungen in Fließtexten: <c>@Eisenschwert</c> wird beim Speichern zu einer stabilen
/// Verknüpfung, und die Referenzansicht findet sie wieder.
/// </summary>
public class MentionTests
{
    private static async Task<Guid> SeedItemAsync(TestDatabase test, string name)
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    private static async Task<Guid> SaveStoryAsync(TestDatabase test, string body)
    {
        var story = test.GetService<StoryService>();
        var context = await story.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Der Aufbruch";
        context.Entity.Body = body;

        await story.SaveEntryAsync(context);
        return context.Entity.Id;
    }

    [Fact]
    public async Task Aus_At_Name_wird_beim_Speichern_eine_Verknuepfung()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Eisenschwert");

        var entryId = await SaveStoryAsync(test, "Er zog @Eisenschwert aus dem Stein.");

        var stored = (await test.GetService<StoryService>().LoadForEditAsync(test.ProjectId, entryId))!.Entity;
        var mention = Assert.Single(ContentMentions.Parse(stored.Body));

        Assert.Equal(itemId, mention.EntityId);
        Assert.Equal(ModuleKeys.Items, mention.ModuleKey);
        Assert.Equal("Eisenschwert", mention.DisplayName);

        // In der Maske steht wieder die lesbare Fassung.
        Assert.Equal("Er zog @Eisenschwert aus dem Stein.", ContentMentions.ToEditable(stored.Body));
    }

    [Fact]
    public async Task Der_laengste_passende_Name_gewinnt()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Eisen");
        var swordId = await SeedItemAsync(test, "Eisenschwert");

        var entryId = await SaveStoryAsync(test, "Das @Eisenschwert glänzte.");

        var stored = (await test.GetService<StoryService>().LoadForEditAsync(test.ProjectId, entryId))!.Entity;

        // Sonst schnappte sich „Eisen“ den Anfang und ließe „schwert“ als losen Text zurück.
        Assert.Equal(swordId, Assert.Single(ContentMentions.Parse(stored.Body)).EntityId);
    }

    [Fact]
    public async Task Ein_unbekannter_Name_bleibt_stehen()
    {
        using var test = new TestDatabase();

        var entryId = await SaveStoryAsync(test, "Er suchte @Irgendwas.");

        var stored = (await test.GetService<StoryService>().LoadForEditAsync(test.ProjectId, entryId))!.Entity;

        // Ein Text ist Text — ein Tippfehler darf ihn nicht verstümmeln.
        Assert.Equal("Er suchte @Irgendwas.", stored.Body);
        Assert.Empty(ContentMentions.Parse(stored.Body));
    }

    [Fact]
    public async Task Die_Referenzansicht_findet_die_Erwaehnung()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Eisenschwert");
        var entryId = await SaveStoryAsync(test, "Er zog @Eisenschwert aus dem Stein.");

        var hit = Assert.Single(await test.GetService<ReferenceService>().FindReferencesAsync(itemId));

        Assert.Equal(entryId, hit.SourceEntityId);
        Assert.Equal(ModuleKeys.Story, hit.SourceModuleKey);
    }

    [Fact]
    public void Markdown_zeichnet_aus_und_maskiert_alles_Uebrige()
    {
        var blocks = SimpleMarkdown.Render("# Titel\n**fett** und *kursiv*\n<script>böse</script>");

        Assert.Equal(3, blocks.Count);
        Assert.Contains("Titel", blocks[0][0].Html, StringComparison.Ordinal);
        Assert.Contains("<b>fett</b>", blocks[1][0].Html, StringComparison.Ordinal);
        Assert.Contains("<i>kursiv</i>", blocks[1][0].Html, StringComparison.Ordinal);

        // Der Text kommt aus einem Eingabefeld — eingebettetes HTML darf nicht durchgehen.
        Assert.Contains("&lt;script&gt;", blocks[2][0].Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", blocks[2][0].Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Erwaehnung_wird_als_eigenes_Stueck_gerendert()
    {
        var id = Guid.NewGuid();
        var text = "Er zog " + ContentMentions.Format(ModuleKeys.Items, id, "Eisenschwert") + " hervor.";

        var segments = Assert.Single(SimpleMarkdown.Render(text));

        var mention = Assert.Single(segments, segment => segment.Mention is not null);

        Assert.Equal(id, mention.Mention!.EntityId);
        Assert.Equal("Eisenschwert", mention.Html);
    }

    [Fact]
    public void Nur_http_Links_werden_verlinkt()
    {
        var safe = SimpleMarkdown.Render("[Doku](https://example.com)")[0][0].Html;
        var unsafeLink = SimpleMarkdown.Render("[böse](javascript:alert(1))")[0][0].Html;

        Assert.Contains("<a href=\"https://example.com\"", safe, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", unsafeLink, StringComparison.Ordinal);
    }
}
