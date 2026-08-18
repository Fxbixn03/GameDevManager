using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Stummschaltung je Health-Check-Fund: „bewusst so“. Stummgeschaltete Funde zählen nicht im
/// Zustandsband und stehen auf der Statistik-Seite in einem eigenen Abschnitt, statt zwischen
/// den offenen zu rauschen.
/// <para>
/// Stummschalten geht nur je <b>Entität und Prüfart</b> — Prüfungen ohne einzelne Entität
/// (Ringe, Bedingungen, eigene Regeln) haben keinen Stummschalter: Ein Ring aus vier Rezepten
/// ist kein „bewusst so“ an einer Stelle.
/// </para>
/// </summary>
public class HealthCheckMuteService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    PermissionGuard guard)
{
    /// <summary>Alle Stummschaltungen eines Projekts — für den Abschnitt der Statistik-Seite.</summary>
    public async Task<List<HealthCheckMute>> GetForProjectAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.HealthCheckMutes
            .AsNoTracking()
            .Where(mute => mute.GameProjectId == projectId)
            .OrderBy(mute => mute.CheckKey)
            .ThenBy(mute => mute.EntityName)
            .ToListAsync(ct);
    }

    /// <summary>Die stummen Funde als Schlüsselmenge — zum Herausfiltern beim Zählen.</summary>
    public async Task<HashSet<(string CheckKey, Guid EntityId)>> GetMutedKeysAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var rows = await db.HealthCheckMutes
            .AsNoTracking()
            .Where(mute => mute.GameProjectId == projectId)
            .Select(mute => new { mute.CheckKey, mute.EntityId })
            .ToListAsync(ct);

        return [.. rows.Select(row => (row.CheckKey, row.EntityId))];
    }

    /// <summary>
    /// Schaltet einen Fund stumm. Der Name geht als Momentaufnahme mit — für die Liste, wie
    /// beim Änderungsprotokoll. Ein zweiter Klick auf denselben Fund ist kein Fehler.
    /// </summary>
    public async Task MuteAsync(
        Guid projectId, string checkKey, Guid entityId, string? entityName, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        if (await db.HealthCheckMutes.AnyAsync(
                mute => mute.GameProjectId == projectId
                    && mute.CheckKey == checkKey
                    && mute.EntityId == entityId, ct))
        {
            return;
        }

        db.HealthCheckMutes.Add(new HealthCheckMute
        {
            GameProjectId = projectId,
            CheckKey = checkKey,
            EntityId = entityId,
            EntityName = entityName
        });

        // Der Schreibschutz greift am SaveChanges von selbst — kein eigener Guard nötig.
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hebt eine Stummschaltung wieder auf — der Fund meldet sich wieder.</summary>
    public async Task UnmuteAsync(
        Guid projectId, string checkKey, Guid entityId, CancellationToken ct = default)
    {
        // Reiner ExecuteDelete-Pfad am WriteGuardInterceptor vorbei — deshalb selbst prüfen.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.HealthCheckMutes
            .Where(mute => mute.GameProjectId == projectId
                && mute.CheckKey == checkKey
                && mute.EntityId == entityId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Räumt Stummschaltungen ab, deren Fund verschwunden ist — kein Leichenbestand: Kehrt
    /// der Fund später zurück, soll er sich wieder melden. Gerufen vom Prüf-Lauf mit den
    /// aktuellen Fund-Entitäten je Prüfart; Prüfarten, die nicht übergeben werden, bleiben
    /// unangetastet.
    /// <para>
    /// Bewusst ohne Rechteprüfung: Das ist Aufräumen im Vorbeigehen, keine Nutzeraktion —
    /// auch ein lesender Blick aufs Dashboard darf veraltete Stummschaltungen fallen lassen.
    /// </para>
    /// </summary>
    public async Task PruneStaleAsync(
        Guid projectId,
        IReadOnlyDictionary<string, HashSet<Guid>> currentFindings,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        List<string> checkKeys = [.. currentFindings.Keys];

        var candidates = await db.HealthCheckMutes
            .AsNoTracking()
            .Where(mute => mute.GameProjectId == projectId && checkKeys.Contains(mute.CheckKey))
            .Select(mute => new { mute.Id, mute.CheckKey, mute.EntityId })
            .ToListAsync(ct);

        List<Guid> stale =
        [
            .. candidates
                .Where(mute => !currentFindings[mute.CheckKey].Contains(mute.EntityId))
                .Select(mute => mute.Id)
        ];

        if (stale.Count > 0)
        {
            await db.HealthCheckMutes
                .Where(mute => stale.Contains(mute.Id))
                .ExecuteDeleteAsync(ct);
        }
    }
}
