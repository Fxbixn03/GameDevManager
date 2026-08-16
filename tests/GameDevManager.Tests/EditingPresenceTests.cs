using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// „Wird gerade bearbeitet von …“: wer welche Maske offen hat. Reiner Arbeitsspeicher, deshalb
/// braucht der Test keine Datenbank — nur eine Uhr, die sich vorstellen lässt.
/// </summary>
public class EditingPresenceTests
{
    private static readonly Guid Entity = Guid.NewGuid();

    /// <summary>
    /// Eine Uhr, die sich vorstellen lässt — selbst geschrieben statt als Testpaket gezogen,
    /// dieselbe Abwägung wie bei den übrigen Kleinteilen des Hauses.
    /// </summary>
    private sealed class StoppedClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now += amount;
    }

    [Fact]
    public void Die_eigene_Sitzung_meldet_sich_nicht_selbst()
    {
        var presence = new EditingPresence();
        var mine = Guid.NewGuid();

        presence.Announce(Entity, mine, "Alrik");

        // „Sie bearbeiten das gerade“ ist keine Auskunft.
        Assert.Empty(presence.Others(Entity, mine));
    }

    [Fact]
    public void Ein_zweiter_Benutzer_erscheint_und_verschwindet_beim_Abmelden()
    {
        var presence = new EditingPresence();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();

        presence.Announce(Entity, mine, "Alrik");
        presence.Announce(Entity, other, "Brida");

        Assert.Equal("Brida", Assert.Single(presence.Others(Entity, mine)).UserName);

        presence.Release(Entity, other);
        Assert.Empty(presence.Others(Entity, mine));
    }

    [Fact]
    public void Zwei_Fenster_desselben_Benutzers_zaehlen_einmal()
    {
        var presence = new EditingPresence();
        var mine = Guid.NewGuid();

        presence.Announce(Entity, mine, "Alrik");
        presence.Announce(Entity, Guid.NewGuid(), "Brida");
        presence.Announce(Entity, Guid.NewGuid(), "Brida");

        Assert.Single(presence.Others(Entity, mine));
    }

    [Fact]
    public void Ohne_Lebenszeichen_verfaellt_ein_Eintrag()
    {
        var time = new StoppedClock();
        var presence = new EditingPresence(time);
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();

        presence.Announce(Entity, mine, "Alrik");
        presence.Announce(Entity, other, "Brida");

        time.Advance(EditingPresence.Timeout + TimeSpan.FromSeconds(1));

        // Ein abgestürzter Browser meldet sich nicht ab — eine Auskunft, die niemand mehr
        // loswird, wäre schlimmer als gar keine.
        Assert.Empty(presence.Others(Entity, mine));
    }

    [Fact]
    public void Ein_Lebenszeichen_haelt_den_Eintrag_am_Leben()
    {
        var time = new StoppedClock();
        var presence = new EditingPresence(time);
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();

        presence.Announce(Entity, mine, "Alrik");
        presence.Announce(Entity, other, "Brida");

        time.Advance(EditingPresence.Timeout - TimeSpan.FromSeconds(10));
        presence.Announce(Entity, other, "Brida");

        time.Advance(EditingPresence.Timeout - TimeSpan.FromSeconds(10));
        Assert.Single(presence.Others(Entity, mine));
    }

    [Fact]
    public void Andere_Entitaeten_bleiben_unberuehrt()
    {
        var presence = new EditingPresence();
        var mine = Guid.NewGuid();

        presence.Announce(Entity, Guid.NewGuid(), "Brida");

        Assert.Empty(presence.Others(Guid.NewGuid(), mine));
    }
}
