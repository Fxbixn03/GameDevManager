namespace GameDevManager.Domain.Entities;

/// <summary>
/// Welche Art von Weltzustand beschrieben wird. Eine echte Spalte und keine Art, weil das
/// Tool danach filtert und das Bedingungssystem je Ausprägung eine eigene
/// <see cref="ConditionKind"/> hat — eine benutzerdefinierte Art wüsste nicht, welche.
/// <para>
/// Die Zahlenwerte stehen in der Datenbank und bleiben fest.
/// </para>
/// </summary>
public enum WorldStateKind
{
    /// <summary>Tageszeit — „Nacht“, „Morgengrauen“, „Mittag“.</summary>
    TimeOfDay = 0,

    /// <summary>Wetter — „Regen“, „Sturm“, „Klar“.</summary>
    Weather = 1,

    /// <summary>Biom — „Wüste“, „Sumpf“, „Hochgebirge“.</summary>
    Biome = 2
}

/// <summary>
/// Ein benannter Weltzustand: eine Tageszeit, ein Wetter oder ein Biom.
/// <para>
/// Alle drei liegen in einem Modul und einer Tabelle, weil sie strukturell dasselbe sind —
/// ein benannter Zustand, an dem Spawns, Shops und Events hängen. Drei Module wären dreimal
/// dieselbe Liste; was sie unterscheidet, ist <see cref="Kind"/>.
/// </para>
/// <para>
/// Wie überall trägt die Entität nur, was das Tool selbst auswertet. Dauer einer Tageszeit,
/// Sichtweite bei Nebel oder Temperaturspanne eines Bioms definiert der Nutzer als Felder
/// an der Art — bei Bedarf je Ausprägung eine eigene („Wetter-Art: Niederschlag“).
/// </para>
/// </summary>
public class WorldState : ContentEntity
{
    public override string ModuleKey => ModuleKeys.World;

    public WorldStateKind Kind { get; set; } = WorldStateKind.TimeOfDay;

    /// <summary>
    /// Reihenfolge innerhalb der Ausprägung. Tageszeiten haben eine natürliche Abfolge, die
    /// alphabetisch verloren ginge — „Abend, Mittag, Morgen, Nacht“ ist keine Tageszeitliste.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Anzeigefarbe für Marker und Abzeichen, „#RRGGBB“. Dasselbe Muster wie bei den
    /// Seltenheiten: Eine Farbe, die jede Ansicht zuverlässig finden muss, steht als Spalte
    /// da und nicht in einem benutzerdefinierten Feld.
    /// </summary>
    public string? Color { get; set; }
}
