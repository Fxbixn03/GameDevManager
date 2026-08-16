namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine angeheftete Entität — die fünf Dinge, an denen jemand diese Woche arbeitet.
/// <para>
/// Das Dashboard-Band „Weiterarbeiten“ zeigt das zuletzt <b>Geänderte</b>; das ist nicht
/// dasselbe wie das absichtlich Angeheftete. Ein Favorit bleibt stehen, auch wenn zwanzig
/// andere Entitäten dazwischen bearbeitet wurden.
/// </para>
/// <para>
/// Werkzeug-Daten wie die Kanban-Boards: nicht im Export, sie überstehen den ersetzenden
/// Import. Angehängt wird wie überall über die GUID plus Modul-Schlüssel — die Entität kann in
/// jedem Modul liegen. Der Benutzer dagegen hängt über einen echten Fremdschlüssel daran: Ein
/// gelöschtes Konto nimmt seine Merkliste mit.
/// </para>
/// </summary>
public class UserPin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppUserId { get; set; }

    public AppUser? User { get; set; }

    /// <summary>Favoriten hängen am Projekt — die Merkliste wechselt mit ihm.</summary>
    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    /// <summary>Modul der angehefteten Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }

    public Guid EntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
