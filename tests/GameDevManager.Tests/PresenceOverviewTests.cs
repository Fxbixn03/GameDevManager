using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die projektweite Präsenz-Ansicht: Benutzer → Entität → seit wann, aufgelöst über die
/// Modul-Quellen — die eigene Sitzung bleibt draußen, Verschwundenes fällt heraus.
/// </summary>
public class PresenceOverviewTests
{
    [Fact]
    public async Task Die_Uebersicht_loest_Namen_auf_und_verschweigt_die_eigene_Arbeit()
    {
        using var test = new TestDatabase();

        Guid itemId;
        await using (var db = test.CreateContext())
        {
            var item = new Item { GameProjectId = test.ProjectId, Name = "Eisenschwert" };
            itemId = item.Id;
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        var presence = test.GetService<EditingPresence>();

        // Brida sitzt am Eisenschwert, der Testbenutzer (wir) an einem anderen — und eine
        // Sitzung hängt an einer GUID, die es nicht (mehr) gibt.
        presence.Announce(itemId, Guid.NewGuid(), "Brida");
        presence.Announce(itemId, Guid.NewGuid(), "Testbenutzer");
        presence.Announce(Guid.NewGuid(), Guid.NewGuid(), "Brida");

        var entries = await test.GetService<PresenceOverviewService>().GetAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("Brida", entry.UserName);
        Assert.Equal("Eisenschwert", entry.EntityName);
        Assert.Equal(GameDevManager.Domain.ModuleKeys.Items, entry.ModuleKey);
    }
}
