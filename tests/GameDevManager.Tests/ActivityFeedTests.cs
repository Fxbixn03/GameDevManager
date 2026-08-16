using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Aktivitäts-Feed: was sich seit dem letzten Besuch getan hat. Ohne eigenen Datenbestand
/// — er liest das Änderungsprotokoll, die Anmerkungen und die Kanban-Karten.
/// </summary>
public class ActivityFeedTests
{
    private static async Task<Guid> SeedUserAsync(TestDatabase test, string name)
    {
        await using var db = test.CreateContext();

        var user = new AppUser
        {
            UserName = name,
            DisplayName = name,
            PasswordHash = string.Empty,
            // Weit in der Vergangenheit: Ohne gesetzte Marke zählt alles seit der Anlage.
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static void SignIn(TestDatabase test, Guid userId, string name) =>
        test.Author.Current = new ChangeAuthor(userId, name);

    private static async Task<Guid> SaveItemAsync(TestDatabase test, string name)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);

        return context.Entity.Id;
    }

    [Fact]
    public async Task Das_eigene_Tun_ist_keine_Nachricht()
    {
        using var test = new TestDatabase();
        var me = await SeedUserAsync(test, "Alrik");

        SignIn(test, me, "Alrik");
        await SaveItemAsync(test, "Schwert");

        // Man war ja dabei.
        Assert.Empty(await test.GetService<ActivityFeedService>().GetFeedAsync(test.ProjectId));
    }

    [Fact]
    public async Task Aenderungen_anderer_werden_je_Entitaet_zusammengefasst()
    {
        using var test = new TestDatabase();
        var me = await SeedUserAsync(test, "Alrik");
        var other = await SeedUserAsync(test, "Brida");

        SignIn(test, other, "Brida");
        var itemId = await SaveItemAsync(test, "Schwert");

        var items = test.GetService<ItemService>();
        for (var round = 0; round < 2; round++)
        {
            var context = await items.LoadForEditAsync(test.ProjectId, itemId);
            context!.Entity.Description = $"Runde {round}";
            await items.SaveItemAsync(context);
        }

        SignIn(test, me, "Alrik");
        var entry = Assert.Single(await test.GetService<ActivityFeedService>().GetFeedAsync(test.ProjectId));

        // Drei Speichervorgänge, eine Nachricht.
        Assert.Equal(ActivityKind.Change, entry.Kind);
        Assert.Equal("Schwert", entry.EntityName);
        Assert.Equal(3, entry.Count);
        Assert.Equal("Brida", Assert.Single(entry.Actors));
    }

    [Fact]
    public async Task Eine_Erwaehnung_erscheint_als_eigene_Sorte()
    {
        using var test = new TestDatabase();
        var me = await SeedUserAsync(test, "Alrik");
        var other = await SeedUserAsync(test, "Brida");

        SignIn(test, other, "Brida");
        var itemId = await SaveItemAsync(test, "Schwert");

        await test.GetService<ContentCommentService>()
            .AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "@Alrik schau dir den Schaden an.");

        SignIn(test, me, "Alrik");
        var feed = await test.GetService<ActivityFeedService>().GetFeedAsync(test.ProjectId);

        var mention = Assert.Single(feed, entry => entry.Kind == ActivityKind.Mention);

        Assert.Equal("Brida", Assert.Single(mention.Actors));
        Assert.Contains("Schaden", mention.Text!, StringComparison.Ordinal);

        // Der Name der Entität kommt aus dem Änderungsprotokoll — die Anmerkung kennt nur ihre GUID.
        Assert.Equal("Schwert", mention.EntityName);
    }

    [Fact]
    public async Task Eine_Erwaehnung_eines_anderen_geht_mich_nichts_an()
    {
        using var test = new TestDatabase();
        var me = await SeedUserAsync(test, "Alrik");
        var other = await SeedUserAsync(test, "Brida");

        SignIn(test, other, "Brida");
        var itemId = await SaveItemAsync(test, "Schwert");

        await test.GetService<ContentCommentService>()
            .AddAsync(test.ProjectId, itemId, ModuleKeys.Items, "@Cedric bitte prüfen.");

        SignIn(test, me, "Alrik");
        var feed = await test.GetService<ActivityFeedService>().GetFeedAsync(test.ProjectId);

        Assert.DoesNotContain(feed, entry => entry.Kind == ActivityKind.Mention);
    }

    [Fact]
    public async Task Als_gelesen_markieren_leert_den_Feed()
    {
        using var test = new TestDatabase();
        var me = await SeedUserAsync(test, "Alrik");
        var other = await SeedUserAsync(test, "Brida");

        SignIn(test, other, "Brida");
        await SaveItemAsync(test, "Schwert");

        SignIn(test, me, "Alrik");
        var feed = test.GetService<ActivityFeedService>();

        Assert.Equal(1, await feed.CountUnreadAsync(test.ProjectId));

        await feed.MarkReadAsync();

        Assert.Equal(0, await feed.CountUnreadAsync(test.ProjectId));

        await using var db = test.CreateContext();
        Assert.NotNull((await db.AppUsers.FirstAsync(user => user.Id == me)).FeedReadAtUtc);
    }

    [Fact]
    public async Task Ohne_Anmeldung_ist_der_Feed_leer()
    {
        using var test = new TestDatabase();

        test.Author.Current = new ChangeAuthor(null, "System");
        await SaveItemAsync(test, "Schwert");

        // „Seit deinem letzten Besuch“ setzt voraus, dass es ein Du gibt.
        Assert.Empty(await test.GetService<ActivityFeedService>().GetFeedAsync(test.ProjectId));
    }
}
