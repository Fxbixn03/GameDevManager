namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Seltenheitsstufe (Gewöhnlich, Selten, Episch, …). Einmal je Projekt definiert und
/// über Referenzfelder in allen Modulen nutzbar — statt die Stufen an jedem Feld als
/// Freitext oder Auswahlliste zu wiederholen.
/// <para>
/// Anders als die übrigen Module hat dieses bewusst <b>keine Arten und keine
/// benutzerdefinierten Felder</b>: Eine Seltenheit ist ein einfacher Nachschlagewert aus
/// Name, Farbe und Rang. Die Farbe muss jede Ansicht, die eine Seltenheit zeigt,
/// zuverlässig finden, und die Stufen haben eine feste Reihenfolge — alphabetisch stünde
/// „Episch“ vor „Gewöhnlich“. Die von <see cref="ContentEntity"/> geerbte Art bleibt
/// ungenutzt (immer <c>null</c>), damit die gemeinsame Modulmechanik — Suche und
/// Auswahlfelder — unverändert funktioniert.
/// </para>
/// </summary>
public class Rarity : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Rarities;

    /// <summary>Anzeigefarbe als Hex-Wert, z. B. „#A335EE“ für Episch.</summary>
    public string? Color { get; set; }

    /// <summary>Rang in der Stufenfolge — kleinere Zahlen sind gewöhnlicher.</summary>
    public int SortOrder { get; set; }
}
