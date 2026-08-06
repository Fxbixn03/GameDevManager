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
    Custom = 6
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
        Kind is ConditionKind.NpcDefeated or ConditionKind.Flag;

    /// <summary>Diese Art bezieht sich auf eine andere Entität.</summary>
    public bool UsesTarget =>
        Kind is ConditionKind.HasItem or ConditionKind.HasCurrency
            or ConditionKind.QuestState or ConditionKind.NpcDefeated;

    /// <summary>Auf welches Modul sich diese Art bezieht — steuert das Auswahlfeld in der Maske.</summary>
    public string? ExpectedTargetModule => Kind switch
    {
        ConditionKind.HasItem => ModuleKeys.Items,
        ConditionKind.HasCurrency => ModuleKeys.Currencies,
        ConditionKind.QuestState => ModuleKeys.Quests,
        ConditionKind.NpcDefeated => ModuleKeys.Npcs,
        _ => null
    };
}
