namespace GameDevManager.Domain.Entities;

/// <summary>
/// Gemeinsame Basis aller fachlichen Inhalte (Items, NPCs, Quests, …).
/// <para>
/// Jedes Modul bekommt seine eigene Tabelle, weil die Module später sehr unterschiedliche
/// Beziehungen brauchen (Rezept-Zutaten, Händler-Angebote, Karten-Marker). Gemeinsam sind
/// nur GUID, Projektzugehörigkeit, Art und die Stammdaten — die restlichen Felder definiert
/// der Nutzer über <see cref="FieldDefinition"/> und <see cref="FieldValue"/>.
/// </para>
/// </summary>
public abstract class ContentEntity : IChangeLogged
{
    /// <summary>Zugleich die Referenz-GUID, über die andere Module diese Entität ansprechen.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Die Art der Entität. <c>null</c> heißt „ohne Art" — erlaubt, damit man schnell erfassen kann.</summary>
    public Guid? ContentTypeId { get; set; }

    public ContentType? ContentType { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Modul der Entität — siehe <see cref="ModuleKeys"/>. Nicht persistiert.</summary>
    public abstract string ModuleKey { get; }
}
