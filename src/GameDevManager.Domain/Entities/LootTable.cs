namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wie die Einträge einer Loot-Table ausgewertet werden. Die Unterscheidung ist nötig, weil
/// beide Verfahren in Spielen üblich sind und die Wahrscheinlichkeiten je nach Verfahren etwas
/// völlig anderes bedeuten.
/// </summary>
public enum LootRollMode
{
    /// <summary>
    /// Jeder Eintrag wird einzeln gewürfelt. Es können mehrere Dinge gleichzeitig fallen oder
    /// gar nichts. Die Summe der Wahrscheinlichkeiten darf über 100 % liegen.
    /// </summary>
    Independent = 0,

    /// <summary>
    /// Es fällt höchstens ein Eintrag; die Wahrscheinlichkeiten teilen sich einen Wurf.
    /// Über 100 % hinaus wären die hinteren Einträge unerreichbar — genau der Health Check
    /// „Loot-Wahrscheinlichkeiten über 100 %“ aus dem Konzept.
    /// </summary>
    SinglePick = 1
}

/// <summary>
/// Eine Loot-Table: welche Items zu welcher Wahrscheinlichkeit in welcher Menge fallen.
/// NPCs und Mobs verweisen darauf; später auch Events als Belohnung.
/// </summary>
public class LootTable : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Loot;

    public LootRollMode RollMode { get; set; } = LootRollMode.Independent;

    public List<LootEntry> Entries { get; set; } = [];
}
