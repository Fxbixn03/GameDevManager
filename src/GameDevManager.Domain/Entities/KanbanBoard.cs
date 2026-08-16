namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Kanban-Board der Projektverwaltung. Beliebig viele je Projekt — „Programmierung“,
/// „Art“, „Release-Plan“.
/// <para>
/// Bewusst <b>keine</b> <see cref="ContentEntity"/> und nicht im Export: Boards sind
/// Werkzeug-Daten wie das Änderungsprotokoll und die Dashboard-Anordnung — sie beschreiben
/// die Arbeit am Spiel, nicht das Spiel. Sie überstehen deshalb auch den ersetzenden Import.
/// </para>
/// </summary>
public class KanbanBoard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<KanbanColumn> Columns { get; set; } = [];
}

/// <summary>Eine Spalte eines Boards — „Offen“, „In Arbeit“, „Fertig“ oder was immer der Nutzer anlegt.</summary>
public class KanbanColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoardId { get; set; }

    public KanbanBoard? Board { get; set; }

    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public List<KanbanCard> Cards { get; set; } = [];
}

/// <summary>Eine Karte auf einem Board.</summary>
public class KanbanCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ColumnId { get; set; }

    public KanbanColumn? Column { get; set; }

    public required string Title { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Wer die Aufgabe übernommen hat. Ein echter Fremdschlüssel wie beim <see cref="UserPin"/>:
    /// Benutzer gehören der Installation, und ein gelöschtes Konto soll keine Karte mit einem
    /// Verweis ins Leere hinterlassen — die Zuweisung fällt dann weg, die Karte bleibt.
    /// </summary>
    public Guid? AssignedUserId { get; set; }

    public AppUser? AssignedUser { get; set; }

    /// <summary>
    /// Fällig am. Ein reines Datum ohne Uhrzeit — eine Aufgabe ist an einem Tag fällig, nicht
    /// um 14:30; gespeichert als <c>DateTime</c>, weil das über alle vier Provider gleich geht.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Farbliche Marke der Karte („#RRGGBB“) — reine Anzeige, wie bei den Seltenheiten.</summary>
    public string? Color { get; set; }

    /// <summary>Kurzes Etikett („Bug“, „Balancing“) — Freitext, kein Verweis auf das Tag-Modul.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Die verknüpfte Entität — Modul plus GUID wie bei den Story-Beteiligten, ohne
    /// Fremdschlüssel, damit jede Art von Inhalt gemeint sein kann.
    /// <para>
    /// Der Gegenzug ist wertvoller als die Karte selbst: In der Bearbeitungsmaske einer Entität
    /// steht damit, welche offenen Aufgaben an ihr hängen.
    /// </para>
    /// </summary>
    public string? TargetModuleKey { get; set; }

    public Guid? TargetEntityId { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
