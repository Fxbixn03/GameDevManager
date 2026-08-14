namespace GameDevManager.Domain.Entities;

/// <summary>
/// Die Spielerfigur. Bewusst mehrere je Projekt möglich — Spiele mit wählbaren Charakteren
/// brauchen je Figur einen Eintrag.
/// <para>
/// Keine <see cref="ContentEntity"/>: Die Arten und benutzerdefinierten Felder des
/// Spieler-Moduls gehören laut Konzept den Skills („eigene Felder für Skills definieren“),
/// und je Modul kann nur eine Entitätsform den Arten-Pool tragen. Sprites funktionieren
/// trotzdem, weil Assets nur über die GUID hängen.
/// </para>
/// </summary>
public class PlayerCharacter : IChangeLogged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Modul der Entität für das Änderungsprotokoll. Nicht persistiert.</summary>
    public string ModuleKey => ModuleKeys.Player;

    /// <summary>GUID-Referenz auf die Klasse der Figur — das Mapping aus dem Klassen-Modul.</summary>
    public Guid? CharacterClassId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ein Skilltree — die benannte Gruppe, in der Skills hängen (z. B. „Kampf“ oder „Magie“).
/// Die Baumstruktur selbst entsteht an den Skills über <see cref="Skill.ParentSkillId"/>.
/// </summary>
public class SkillTree : IChangeLogged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Modul der Entität für das Änderungsprotokoll. Nicht persistiert.</summary>
    public string ModuleKey => ModuleKeys.Player;
}

/// <summary>
/// Ein Skill. Das Konzept verlangt je Skill: was er heißt und macht (Name/Beschreibung) und
/// wie man ihn erreicht — „ob dafür Punkte oder Ressourcen ausgegeben werden müssen“.
/// Beides ist strukturell abgebildet; alles Weitere definiert der Nutzer als Felder der
/// Skill-Art.
/// </summary>
public class Skill : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Player;

    /// <summary>Der Baum, zu dem der Skill gehört. <c>null</c> heißt „noch keinem zugeordnet“.</summary>
    public Guid? SkillTreeId { get; set; }

    /// <summary>
    /// Der Skill, der vorher freigeschaltet sein muss — daraus entsteht die Baumstruktur.
    /// GUID statt Fremdschlüssel: er liefe im Kreis auf dieselbe Tabelle, der Service prüft
    /// beim Speichern Baum und Zyklenfreiheit.
    /// </summary>
    public Guid? ParentSkillId { get; set; }

    /// <summary>Kosten in Skill-Punkten, falls der Skill über Punkte erreicht wird.</summary>
    public double? CostPoints { get; set; }

    /// <summary>GUID-Referenz auf das Item, falls der Skill Ressourcen kostet.</summary>
    public Guid? CostItemId { get; set; }

    /// <summary>Wie viele davon.</summary>
    public int? CostItemAmount { get; set; }
}
