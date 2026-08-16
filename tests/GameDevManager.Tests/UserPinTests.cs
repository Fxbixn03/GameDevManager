using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Favoriten: die paar Entitäten, an denen jemand gerade arbeitet. Anders als das
/// „Weiterarbeiten“ des Dashboards bleiben sie stehen, bis man sie wieder löst.
/// </summary>
public class UserPinTests
{
    /// <summary>Ein Konto anlegen — der Fremdschlüssel des Favoriten braucht eine echte Zeile.</summary>
    private static async Task<Guid> SeedUserAsync(TestDatabase test, string name = "alrik")
    {
        await using var db = test.CreateContext();

        var user = new AppUser
        {
            UserName = name,
            DisplayName = name,
            PasswordHash = string.Empty
        };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        test.Author.Current = new ChangeAuthor(user.Id, name);
        return user.Id;
    }

    private static async Task<Guid> SeedItemAsync(TestDatabase test, string name)
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    [Fact]
    public async Task Der_Stern_schaltet_um()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var itemId = await SeedItemAsync(test, "Schwert");

        var pins = test.GetService<UserPinService>();

        Assert.False(await pins.IsPinnedAsync(itemId));

        Assert.True(await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, itemId));
        Assert.True(await pins.IsPinnedAsync(itemId));

        Assert.False(await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, itemId));
        Assert.False(await pins.IsPinnedAsync(itemId));
    }

    [Fact]
    public async Task Die_Liste_loest_Namen_auf_und_zeigt_das_zuletzt_Angeheftete_zuerst()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);

        var first = await SeedItemAsync(test, "Schwert");
        var second = await SeedItemAsync(test, "Axt");

        var pins = test.GetService<UserPinService>();
        await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, first);
        await Task.Delay(5);
        await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, second);

        var entries = await pins.GetPinnedAsync(test.ProjectId);

        Assert.Equal(new[] { "Axt", "Schwert" }, entries.Select(entry => entry.Name));
        Assert.All(entries, entry => Assert.Equal(ModuleKeys.Items, entry.ModuleKey));
    }

    [Fact]
    public async Task Ein_Favorit_auf_Geloeschtes_raeumt_sich_selbst_ab()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var itemId = await SeedItemAsync(test, "Schwert");

        var pins = test.GetService<UserPinService>();
        await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, itemId);

        await test.GetService<ItemService>().DeleteItemAsync(itemId);

        // Eine Merkliste, die auf Gelöschtes zeigt, ist keine.
        Assert.Empty(await pins.GetPinnedAsync(test.ProjectId));

        await using var db = test.CreateContext();
        Assert.Empty(await db.UserPins.ToListAsync());
    }

    [Fact]
    public async Task Ohne_Anmeldung_gibt_es_keine_Merkliste()
    {
        using var test = new TestDatabase();
        var itemId = await SeedItemAsync(test, "Schwert");

        // Kein Benutzer hinter dem Urheber — eine Liste ohne Besitzer gehörte niemandem.
        test.Author.Current = new ChangeAuthor(null, "System");

        var pins = test.GetService<UserPinService>();

        Assert.False(await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, itemId));
        Assert.Empty(await pins.GetPinnedAsync(test.ProjectId));
    }

    [Fact]
    public async Task Favoriten_eines_anderen_Benutzers_bleiben_draussen()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test, "alrik");
        var itemId = await SeedItemAsync(test, "Schwert");

        var pins = test.GetService<UserPinService>();
        await pins.ToggleAsync(test.ProjectId, ModuleKeys.Items, itemId);

        await SeedUserAsync(test, "brida");

        Assert.False(await pins.IsPinnedAsync(itemId));
        Assert.Empty(await pins.GetPinnedAsync(test.ProjectId));
    }
}
