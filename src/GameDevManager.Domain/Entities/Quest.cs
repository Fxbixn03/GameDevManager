namespace GameDevManager.Domain.Entities;

/// <summary>
/// Die drei Quest-Formen des Konzepts. Eine echte Spalte und keine Art, weil das Tool
/// danach filtert — Arten bleiben frei für die fachliche Einteilung des Nutzers
/// (Botengang, Eskorte, Sammelquest, …).
/// </summary>
public enum QuestKind
{
    /// <summary>Hauptmission — definiert den Storyverlauf.</summary>
    MainMission = 0,

    /// <summary>Nebenmission — Nebenhandlungen mit kleinen Belohnungen.</summary>
    SideMission = 1,

    /// <summary>Event — tritt zufällig auf. Das Event-Modul verfeinert diese Form später.</summary>
    Event = 2
}

/// <summary>
/// Eine Quest bzw. Mission. Baut laut Konzept auf dem Story-Modul auf und kann mit Story,
/// NPCs und Dialogen verknüpft werden — alles GUID-Referenzen ohne Fremdschlüssel über die
/// Modulgrenze.
/// <para>
/// Belohnungen, Questtexte je Schritt und Ähnliches definiert der Nutzer als Felder an der
/// Quest-Art. Verfügbarkeit und Abschluss laufen über das Bedingungssystem
/// (<see cref="ConditionSlots.Availability"/> und <see cref="ConditionSlots.Completion"/>).
/// </para>
/// </summary>
public class Quest : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Quests;

    public QuestKind Kind { get; set; } = QuestKind.SideMission;

    /// <summary>Der NPC, der die Quest vergibt — „die der Spieler von NPCs erhalten kann“.</summary>
    public Guid? GiverNpcId { get; set; }

    /// <summary>Der Story-Abschnitt, an den die Quest angelehnt ist.</summary>
    public Guid? StoryEntryId { get; set; }

    /// <summary>Der Dialog, aus dem die Quest hervorgeht.</summary>
    public Guid? DialogueId { get; set; }
}
