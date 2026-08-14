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

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
