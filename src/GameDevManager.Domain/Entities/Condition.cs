namespace GameDevManager.Domain.Entities;

/// <summary>
/// Was eine Bedingung prüft. Die Zahlenwerte stehen in der Datenbank und bleiben fest; neue
/// Arten werden hinten angehängt.
/// </summary>
public enum ConditionKind
{
    /// <summary>Der Spieler besitzt ein Item in einer bestimmten Menge.</summary>
    HasItem = 0,

    /// <summary>Der Spieler besitzt eine Währung in einer bestimmten Menge.</summary>
    HasCurrency = 1,

    /// <summary>Eine Quest steht in einem bestimmten Zustand.</summary>
    QuestState = 2,

    /// <summary>Ein NPC oder Mob wurde besiegt.</summary>
    NpcDefeated = 3,

    /// <summary>Ein benannter Schalter der Story ist gesetzt.</summary>
    Flag = 4,

    /// <summary>Die Stufe des Spielers.</summary>
    PlayerLevel = 5,

    /// <summary>Frei beschrieben — für alles, was das Tool noch nicht kennt.</summary>
    Custom = 6,

    /// <summary>Es ist eine bestimmte Tageszeit — siehe <see cref="WorldStateKind.TimeOfDay"/>.</summary>
    TimeOfDay = 7,

    /// <summary>Es herrscht ein bestimmtes Wetter — siehe <see cref="WorldStateKind.Weather"/>.</summary>
    Weather = 8,

    /// <summary>Der Spieler ist in einem bestimmten Biom — siehe <see cref="WorldStateKind.Biome"/>.</summary>
    Biome = 9,

    /// <summary>
    /// Eine andere Entität ist bereits freigeschaltet. Die Art, die den Freischaltungs-Graphen
    /// trägt: Sie verweist auf beliebige Module, deshalb steht das Zielmodul an der Bedingung
    /// statt an der Art.
    /// </summary>
    Unlocked = 10
}

/// <summary>Wie ein Zahlenwert verglichen wird.</summary>
public enum ComparisonOperator
{
    AtLeast = 0,
    GreaterThan = 1,
    Equal = 2,
    AtMost = 3,
    LessThan = 4,
    NotEqual = 5
}

/// <summary>
/// Eine einzelne Bedingung.
/// <para>
/// Welche Spalten getragen werden, hängt an <see cref="Kind"/> — wie bei den Feldwerten:
/// mengenbezogene Arten nutzen <see cref="Operator"/> und <see cref="NumberValue"/>, Ja/Nein-Arten
/// <see cref="BooleanValue"/>, und der Bezug auf eine andere Entität läuft wie überall über eine
/// GUID.
/// </para>
/// </summary>
public class Condition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConditionSetId { get; set; }

    public ConditionSet? ConditionSet { get; set; }

    public ConditionKind Kind { get; set; } = ConditionKind.HasItem;

    /// <summary>Modul der bezogenen Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public string? TargetModuleKey { get; set; }

    /// <summary>GUID der bezogenen Entität, ohne Fremdschlüssel wie alle modulübergreifenden Verweise.</summary>
    public Guid? TargetEntityId { get; set; }

    public ComparisonOperator Operator { get; set; } = ComparisonOperator.AtLeast;

    /// <summary>Menge oder Stufe — bei <see cref="ConditionKind.HasItem"/>, <c>HasCurrency</c> und <c>PlayerLevel</c>.</summary>
    public double? NumberValue { get; set; }

    /// <summary>Soll zutreffen oder ausdrücklich nicht — bei <see cref="ConditionKind.NpcDefeated"/> und <c>Flag</c>.</summary>
    public bool? BooleanValue { get; set; }

    /// <summary>Name des Schalters, Quest-Zustand oder freie Beschreibung.</summary>
    public string? TextValue { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Diese Art vergleicht eine Menge und braucht Operator und Zahl.</summary>
    public bool UsesNumber =>
        Kind is ConditionKind.HasItem or ConditionKind.HasCurrency or ConditionKind.PlayerLevel;

    /// <summary>Diese Art ist eine Ja/Nein-Frage.</summary>
    public bool UsesBoolean =>
        Kind is ConditionKind.NpcDefeated or ConditionKind.Flag or ConditionKind.Unlocked
            or ConditionKind.TimeOfDay or ConditionKind.Weather or ConditionKind.Biome;

    /// <summary>Diese Art bezieht sich auf eine andere Entität.</summary>
    public bool UsesTarget =>
        Kind is ConditionKind.HasItem or ConditionKind.HasCurrency
            or ConditionKind.QuestState or ConditionKind.NpcDefeated or ConditionKind.Unlocked
            or ConditionKind.TimeOfDay or ConditionKind.Weather or ConditionKind.Biome;

    /// <summary>
    /// Auf welches Modul sich diese Art bezieht — steuert das Auswahlfeld in der Maske.
    /// <c>null</c> heißt „die Art legt es nicht fest“; dann wählt der Nutzer das Modul selbst,
    /// siehe <see cref="ChoosesTargetModule"/>.
    /// </summary>
    public string? ExpectedTargetModule => Kind switch
    {
        ConditionKind.HasItem => ModuleKeys.Items,
        ConditionKind.HasCurrency => ModuleKeys.Currencies,
        ConditionKind.QuestState => ModuleKeys.Quests,
        ConditionKind.NpcDefeated => ModuleKeys.Npcs,
        ConditionKind.TimeOfDay or ConditionKind.Weather or ConditionKind.Biome => ModuleKeys.World,
        _ => null
    };

    /// <summary>
    /// Das Zielmodul wählt der Nutzer. Bisher nur bei <see cref="ConditionKind.Unlocked"/>:
    /// Freigeschaltet werden kann alles — ein Skill, ein Rezept, ein Gebiet —, und ein fest
    /// verdrahtetes Modul hieße, den Freischaltungs-Graphen auf eine Sorte Inhalt zu verengen.
    /// </summary>
    public bool ChoosesTargetModule => Kind == ConditionKind.Unlocked;

    /// <summary>
    /// Das Modul, aus dem die Zielentität stammt: bei fest zugeordneten Arten das erwartete,
    /// bei frei wählbaren das gespeicherte. Die Maske füllt ihr Auswahlfeld hieraus.
    /// </summary>
    public string? TargetModule => ExpectedTargetModule ?? TargetModuleKey;

    /// <summary>
    /// Diese Ausprägung passt zu einem Weltzustand dieser Art — die Maske filtert die
    /// Auswahl danach, damit unter „Wetter“ keine Biome stehen.
    /// </summary>
    public WorldStateKind? ExpectedWorldStateKind => Kind switch
    {
        ConditionKind.TimeOfDay => WorldStateKind.TimeOfDay,
        ConditionKind.Weather => WorldStateKind.Weather,
        ConditionKind.Biome => WorldStateKind.Biome,
        _ => null
    };
}
