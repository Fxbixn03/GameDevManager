using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile der Papierkorb-Liste.</summary>
public sealed record RecycleBinRow(
    Guid Id,
    string ModuleKey,
    Guid EntityId,
    string EntityName,
    DateTime DeletedAtUtc,
    string DeletedBy,
    bool IsBlocked);

/// <summary>
/// Der Papierkorb (F24): Wer eine Entität löscht, soll sie zurückholen können, ohne den letzten
/// Exportstand einzuspielen und alles seitdem zu verlieren.
/// <para>
/// <b>Kein Soft-Delete-Schalter.</b> Der zöge eine Filterbedingung durch jede Abfrage des
/// gesamten Bestands — Listen, Suche, Referenzansicht, Export, Health Checks — und wäre die
/// Sorte Änderung, die man an einer Stelle vergisst. Stattdessen dieselbe Strecke wie beim
/// Duplizieren, nur rückwärts: serialisieren, aufbewahren, mit den <b>originalen</b> GUIDs
/// zurücklesen.
/// </para>
/// <para>
/// Erfasst wird in <see cref="EntityCleanup.DeleteForEntityAsync"/> — der einen Stelle, durch
/// die jeder Löschpfad läuft und die das <c>DbSet</c> ohnehin schon in der Hand hat. Ein
/// Aufruf je Modul-Dienst wäre der, den ein neues Modul vergisst.
/// </para>
/// </summary>
public class RecycleBinService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    RecycleBinOptions options,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public RecycleBinOptions Options => options;

    /// <summary>
    /// Die Einträge eines Projekts, jüngste zuerst. <c>IsBlocked</c> heißt: Unter dieser GUID
    /// steht schon wieder etwas — das Wiederherstellen liefe in einen Schlüsselkonflikt.
    /// </summary>
    public async Task<List<RecycleBinRow>> GetEntriesAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var entries = await db.RecycleBinEntries
            .AsNoTracking()
            .Where(entry => entry.GameProjectId == projectId)
            .OrderByDescending(entry => entry.DeletedAtUtc)
            .ToListAsync(ct);

        var rows = new List<RecycleBinRow>();

        foreach (var entry in entries)
        {
            rows.Add(new RecycleBinRow(
                entry.Id,
                entry.ModuleKey,
                entry.EntityId,
                entry.EntityName,
                entry.DeletedAtUtc,
                entry.DeletedBy,
                await ExistsAsync(db, projectId, entry, ct)));
        }

        return rows;
    }

    private async Task<bool> ExistsAsync(
        GameDevManagerDbContext db, Guid projectId, RecycleBinEntry entry, CancellationToken ct)
    {
        var source = sources.FirstOrDefault(s => s.ModuleKey == entry.ModuleKey);

        if (source is null)
        {
            return false;
        }

        var existing = await source.GetEntitiesAsync(db, projectId, ct);
        return existing.Any(entity => entity.Id == entry.EntityId);
    }

    /// <summary>
    /// Holt einen Eintrag zurück: Die Entität entsteht mit ihrer ursprünglichen GUID wieder,
    /// samt Kind-Sammlungen, Feldwerten, individuellen Feldern und Bedingungen.
    /// <para>
    /// <b>Sprites kommen nicht mit</b> — beim Löschen verschwinden auch die Dateien, und die
    /// ließen sich aus einer Datenbankzeile nicht wiederherstellen. Wer das braucht, geht über
    /// einen Exportstand; der Papierkorb ist für den Fehlklick da, nicht für die Sicherung.
    /// </para>
    /// </summary>
    public async Task RestoreAsync(Guid entryId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var entry = await db.RecycleBinEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new ContentValidationException(messages["RecycleBinEntryGone"]);

        var source = sources.FirstOrDefault(s => s.ModuleKey == entry.ModuleKey)
            ?? throw new ContentValidationException(messages["RecycleBinModuleUnknown", entry.ModuleKey]);

        if (await ExistsAsync(db, entry.GameProjectId, entry, ct))
        {
            throw new ContentValidationException(messages["RecycleBinIdTaken", entry.EntityName]);
        }

        source.Restore(db, entry.Payload);

        // Der Eintrag fällt mit dem Wiederherstellen weg: Er beschreibt einen Zustand, den es
        // nicht mehr gibt, und ein zweites Wiederherstellen liefe in den Schlüsselkonflikt.
        db.RecycleBinEntries.Remove(entry);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Wirft einen Eintrag endgültig weg.</summary>
    public async Task PurgeAsync(Guid entryId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.RecycleBinEntries.Where(entry => entry.Id == entryId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Leert den Papierkorb eines Projekts.</summary>
    public async Task<int> EmptyAsync(Guid projectId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.RecycleBinEntries
            .Where(entry => entry.GameProjectId == projectId)
            .ExecuteDeleteAsync(ct);
    }

    // ------------------------------------------------------------------------ Aufbewahrung

    /// <summary>
    /// Räumt einen Papierkorb nach den eingestellten Grenzen auf. Angestoßen vom
    /// Wartungslauf, der auch das Änderungsprotokoll kürzt — dieselbe Überlegung: Bei jedem
    /// Löschen aufzuräumen belastete den Vorgang mit einer Abfrage, die fast immer nichts
    /// findet.
    /// </summary>
    public async Task<int> PruneAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!options.HasRetentionRule)
        {
            return 0;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var removed = 0;

        if (options.MaxAgeDays > 0)
        {
            var limit = DateTime.UtcNow.AddDays(-options.MaxAgeDays);

            removed += await db.RecycleBinEntries
                .Where(entry => entry.GameProjectId == projectId && entry.DeletedAtUtc < limit)
                .ExecuteDeleteAsync(ct);
        }

        if (options.MaxPerProject > 0)
        {
            // Über die GUIDs der überzähligen Einträge und nicht über einen Zeitstempel als
            // Grenze: Beim Löschen mehrerer Entitäten entstehen Einträge in derselben Sekunde,
            // und eine Grenze „älter als“ träfe von ihnen mal alle, mal keinen.
            var doomed = await db.RecycleBinEntries
                .Where(entry => entry.GameProjectId == projectId)
                .OrderByDescending(entry => entry.DeletedAtUtc)
                .Skip(options.MaxPerProject)
                .Select(entry => entry.Id)
                .ToListAsync(ct);

            foreach (var block in doomed.Chunk(500))
            {
                removed += await db.RecycleBinEntries
                    .Where(entry => block.Contains(entry.Id))
                    .ExecuteDeleteAsync(ct);
            }
        }

        return removed;
    }

    /// <summary>Derselbe Lauf über alle Projekte — für den Hintergrunddienst.</summary>
    public async Task<int> PruneAllProjectsAsync(CancellationToken ct = default)
    {
        if (!options.HasRetentionRule)
        {
            return 0;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        // Archivierte Projekte überspringt der Lauf: Ihr Bestand soll vollständig gehalten
        // werden — auch der Papierkorb.
        var projectIds = await db.GameProjects
            .Where(project => !project.IsArchived)
            .Select(project => project.Id)
            .ToListAsync(ct);

        var removed = 0;

        foreach (var projectId in projectIds)
        {
            removed += await PruneAsync(projectId, ct);
        }

        return removed;
    }
}
