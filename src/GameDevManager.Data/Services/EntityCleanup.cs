using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Räumt alles ab, was über die GUID einer Entität an ihr hängt statt über einen
/// Fremdschlüssel — Feldwerte, individuelle Felder, Tag-Zuweisungen, Übersetzungen und
/// Bedingungssätze.
/// <para>
/// Diese Dinge sind bewusst modulübergreifend und deshalb ohne Fremdschlüssel angebunden; sie
/// fallen beim Löschen also nicht von selbst mit. An einer Stelle gebündelt, damit kein Modul
/// eine Art davon vergisst — Assets bleiben getrennt, weil dabei auch Dateien verschwinden
/// und das nicht zurückrollbar ist.
/// </para>
/// </summary>
public static class EntityCleanup
{
    /// <summary>Entfernt alles, was an der GUID dieser Entität hängt.</summary>
    public static Task DeleteForEntityAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        DeleteForEntitiesAsync(db, [entityId], ct);

    /// <summary>
    /// Dasselbe für mehrere GUIDs auf einmal. Nötig für Entitäten mit Teilobjekten, die eigene
    /// GUIDs haben und eigene Bedingungen tragen können — etwa die Posten eines Händlers.
    /// </summary>
    public static async Task DeleteForEntitiesAsync(
        GameDevManagerDbContext db, IReadOnlyCollection<Guid> entityIds, CancellationToken ct)
    {
        if (entityIds.Count == 0)
        {
            return;
        }

        // Zuerst die individuellen Felder — deren Werte fallen über den Fremdschlüssel mit.
        await db.FieldDefinitions
            .Where(field => field.OwnerEntityId != null && entityIds.Contains(field.OwnerEntityId.Value))
            .ExecuteDeleteAsync(ct);

        await db.FieldValues
            .Where(value => entityIds.Contains(value.OwnerEntityId))
            .ExecuteDeleteAsync(ct);

        // Tag-Zuweisungen hängen ebenfalls nur über die GUID an der Entität.
        await db.ContentTagAssignments
            .Where(assignment => entityIds.Contains(assignment.TargetEntityId))
            .ExecuteDeleteAsync(ct);

        // Übersetzungen ebenso — sie zeigen auf Name, Beschreibung und Textfelder genau
        // dieser Entität und wären ohne sie sinnlos.
        await db.ContentTranslations
            .Where(translation => entityIds.Contains(translation.OwnerEntityId))
            .ExecuteDeleteAsync(ct);

        await ConditionService.DeleteForOwnersAsync(db, entityIds, ct);
    }
}
