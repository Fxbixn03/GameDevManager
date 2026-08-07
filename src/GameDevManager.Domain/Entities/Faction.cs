namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Fraktion des Spiels. Das Konzept baut sie auf dem NPC-Modul auf: NPCs werden
/// Mitglieder und können dabei eine Rolle bzw. einen Rang bekommen.
/// <para>
/// Strukturell trägt die Fraktion nur ihre Mitgliederliste — alles Weitere (Gesinnung,
/// Hauptsitz, Ansehen) definiert der Nutzer als Felder an der Fraktions-Art. Gebiete auf
/// der Karte entstehen im Karten-Modul über Markierungen, die auf die Fraktion zeigen.
/// </para>
/// </summary>
public class Faction : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Factions;

    public List<FactionMember> Members { get; set; } = [];
}

/// <summary>
/// Die Mitgliedschaft eines NPCs in einer Fraktion. Der NPC hängt — wie überall über die
/// Modulgrenze hinweg — nur über seine GUID daran, ohne Fremdschlüssel.
/// </summary>
public class FactionMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FactionId { get; set; }

    public Faction? Faction { get; set; }

    /// <summary>GUID-Referenz auf den NPC — im Konzept: „Ein NPC kann zu einer Fraktion hinzugefügt werden.“</summary>
    public Guid NpcId { get; set; }

    /// <summary>Rolle bzw. Rang in der Fraktion — „Eine Fraktion kann Rollen an NPCs vergeben.“</summary>
    public string? Role { get; set; }

    public int SortOrder { get; set; }
}
