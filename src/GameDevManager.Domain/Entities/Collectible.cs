namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Sammelobjekt — Statuen, Notizen und Ähnliches, die der Spieler sammeln kann.
/// Das Konzept verlangt hier ausdrücklich eigene Felder; strukturell braucht das Objekt
/// selbst nichts weiter. Fundorte werden im Karten-Modul markiert, Sammel-Belohnungen
/// hängen als Bedingung an Achievements oder Quests.
/// </summary>
public class Collectible : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Collectibles;
}
