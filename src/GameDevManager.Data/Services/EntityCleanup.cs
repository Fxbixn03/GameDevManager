using GameDevManager.Domain.Entities;
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
    /// <summary>
    /// Entfernt alles, was an der GUID einer <b>Entität</b> hängt, und löst zuvor die
    /// Varianten auf, die sie als Vorbild haben.
    /// <para>
    /// Diese Überladung nimmt das <c>DbSet</c> und nicht nur die GUID: Das Vorbild einer
    /// Variante steht in der Tabelle des Moduls, und die kennt nur der Typ. Sie ist der Weg
    /// zum Löschen einer Entität — die GUID-Fassung heißt bewusst
    /// <see cref="DeleteForSubObjectsAsync"/> und ist für Teilobjekte da, damit niemand die
    /// Variantenauflösung versehentlich umgeht.
    /// </para>
    /// </summary>
    public static async Task DeleteForEntityAsync<TEntity>(
        GameDevManagerDbContext db, DbSet<TEntity> set, Guid entityId,
        IReadOnlyCollection<Guid>? subObjectIds, CancellationToken ct)
        where TEntity : ContentEntity
    {
        await CaptureForRecycleBinAsync(db, set, entityId, ct);
        await DissolveVariantsAsync(db, set, entityId, ct);

        IReadOnlyCollection<Guid> owners = subObjectIds is null or { Count: 0 }
            ? [entityId]
            : [entityId, .. subObjectIds];

        await DeleteForSubObjectsAsync(db, owners, ct);
    }

    /// <summary>
    /// Legt den Papierkorb-Eintrag an, bevor irgendetwas verschwindet.
    /// <para>
    /// Hier und nicht in den gut zwanzig Modul-Diensten: Diese Methode hat das <c>DbSet</c>
    /// ohnehin schon in der Hand, und ein Aufruf je Dienst wäre der, den ein neues Modul
    /// vergisst — dieselbe Überlegung, aus der es <see cref="EntityCleanup"/> überhaupt gibt.
    /// </para>
    /// <para>
    /// <b>Vor</b> dem Auflösen der Varianten: Danach stünden deren übernommene Werte doppelt im
    /// Baum, einmal beim Vorbild und einmal bei der Variante. Und vor dem Löschen ohnehin —
    /// hinterher gäbe es nichts mehr zu erfassen.
    /// </para>
    /// </summary>
    private static async Task CaptureForRecycleBinAsync<TEntity>(
        GameDevManagerDbContext db, DbSet<TEntity> set, Guid entityId, CancellationToken ct)
        where TEntity : ContentEntity
    {
        if (!db.RecycleBinEnabled)
        {
            return;
        }

        IQueryable<TEntity> query = set.AsNoTracking();

        foreach (var navigation in db.Model.FindEntityType(typeof(TEntity))!
                     .GetNavigations()
                     .Where(navigation => navigation.IsCollection))
        {
            query = query.Include(navigation.Name);
        }

        var doomed = await query.FirstOrDefaultAsync(entity => entity.Id == entityId, ct);

        if (doomed is null)
        {
            return;
        }

        db.RecycleBinEntries.Add(new RecycleBinEntry
        {
            GameProjectId = doomed.GameProjectId,
            ModuleKey = doomed.ModuleKey,
            EntityId = doomed.Id,
            EntityName = doomed.Name,
            Payload = await EntityDuplication.CaptureAsync(db, doomed, ct)
            // DeletedBy bleibt leer — der ChangeLogInterceptor trägt den Benutzer nach.
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Löst die Varianten auf, deren Vorbild gleich verschwindet: Sie <b>übernehmen dessen
    /// Werte als eigene</b> und rücken in der Kette eine Stufe vor.
    /// <para>
    /// Die Alternative wäre, den Verweis einfach zu leeren — dann verlöre die Variante still
    /// jeden geerbten Wert, und ein Löschklick am Vorbild änderte den halben Bestand. So bleibt
    /// der Stand exakt erhalten: Was die Variante selbst setzt, bleibt ihres; was sie geerbt
    /// hat, wird ihres; was von weiter oben kam, erbt sie weiterhin.
    /// </para>
    /// </summary>
    private static async Task DissolveVariantsAsync<TEntity>(
        GameDevManagerDbContext db, DbSet<TEntity> set, Guid entityId, CancellationToken ct)
        where TEntity : ContentEntity
    {
        var variants = await set
            .Where(entity => entity.BasedOnId == entityId)
            .ToListAsync(ct);

        if (variants.Count == 0)
        {
            return;
        }

        var doomed = await set.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == entityId, ct);

        var doomedValues = await db.FieldValues
            .AsNoTracking()
            .Where(value => value.OwnerEntityId == entityId)
            .ToListAsync(ct);

        foreach (var variant in variants)
        {
            // Eine Stufe vor: Was das Vorbild selbst geerbt hat, erbt die Variante weiter.
            variant.BasedOnId = doomed?.BasedOnId;

            if (doomedValues.Count == 0)
            {
                continue;
            }

            var own = await db.FieldValues
                .Where(value => value.OwnerEntityId == variant.Id)
                .Select(value => value.FieldDefinitionId)
                .ToListAsync(ct);

            foreach (var inherited in doomedValues.Where(v => !own.Contains(v.FieldDefinitionId)))
            {
                var copy = new FieldValue
                {
                    FieldDefinitionId = inherited.FieldDefinitionId,
                    OwnerEntityId = variant.Id,
                    OwnerModuleKey = variant.ModuleKey
                };

                ContentFields.CopyValues(inherited, copy);
                db.FieldValues.Add(copy);
            }
        }

        // Sofort und nicht erst mit dem Löschen: Die Werte des Vorbilds fallen gleich darauf
        // über DeleteForSubObjectsAsync weg, und dann wäre nichts mehr zu kopieren.
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Entfernt alles, was an GUIDs hängt, die <b>keine ContentEntity</b> sind: Teilobjekte
    /// (Händler-Posten, Dialogzeilen, Quest-Ziele, Spawn-Regeln) und die wenigen Besitzer
    /// außerhalb der Basisklasse (<see cref="PlayerCharacter"/>, <see cref="SkillTree"/>).
    /// Sie alle können kein Vorbild einer Variante sein — deshalb braucht es hier keinen Typ.
    /// </summary>
    public static async Task DeleteForSubObjectsAsync(
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

        // Anmerkungen ebenso — sie sind Werkzeug-Daten, aber an einer Entität, die es nicht
        // mehr gibt, wäre eine Anmerkung nur noch ein Rätsel.
        await db.ContentComments
            .Where(comment => entityIds.Contains(comment.OwnerEntityId))
            .ExecuteDeleteAsync(ct);

        await ConditionService.DeleteForOwnersAsync(db, entityIds, ct);
    }
}
