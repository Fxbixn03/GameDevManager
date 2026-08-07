namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Achievement, „z. B. sowas wie die Steam-Achievements“. Das Icon ist das primäre
/// Sprite, die Freischaltung läuft über das Bedingungssystem
/// (<see cref="ConditionSlots.Unlock"/>) — Punkte, Seltenheit und Ähnliches definiert der
/// Nutzer als Felder an der Achievement-Art.
/// </summary>
public class Achievement : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Achievements;

    /// <summary>
    /// Verstecktes Achievement: Name und Beschreibung bleiben im Spiel verborgen, bis es
    /// freigeschaltet ist — der übliche Spoiler-Schutz der Steam-Achievements.
    /// </summary>
    public bool IsSecret { get; set; }
}
