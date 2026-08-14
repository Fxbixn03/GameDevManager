using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Loot-Tables samt ihrer Einträge und benutzerdefinierten Feldwerte.
/// </summary>
public class LootService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<LootTableListRow>> GetTablesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.LootTables
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name)
            .Select(t => new LootTableListRow(
                t.Id,
                t.Name,
                t.Description,
                t.RollMode,
                t.ContentTypeId,
                t.ContentType!.Name,
                t.Entries.Count,
                t.Entries.Sum(entry => (double?)entry.Chance) ?? 0,
                db.Npcs.Count(npc => npc.LootTableId == t.Id),
                t.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Loot-Tables, in denen ein Item vorkommt — für die Item-Maske und die Frage, ob ein
    /// Item überhaupt eine Bezugsquelle hat.
    /// </summary>
    public async Task<List<LootSourceForItem>> GetTablesForItemAsync(
        Guid projectId, Guid itemId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.LootEntries
            .AsNoTracking()
            .Where(entry => entry.ItemId == itemId && entry.LootTable!.GameProjectId == projectId)
            .OrderBy(entry => entry.LootTable!.Name)
            .Select(entry => new LootSourceForItem(
                entry.LootTableId,
                entry.LootTable!.Name,
                entry.Chance,
                entry.MinQuantity,
                entry.MaxQuantity))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Der Health Check des Konzepts: Tabellen, bei denen ein einzelner Wurf verteilt wird und
    /// die Wahrscheinlichkeiten zusammen über 100 % liegen. Die hinteren Einträge wären dann
    /// unerreichbar. Bei unabhängigen Würfen ist eine Summe über 100 % dagegen normal und wird
    /// nicht gemeldet.
    /// </summary>
    public async Task<List<LootTableListRow>> FindOverfullTablesAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var tables = await GetTablesAsync(projectId, ct);

        return
        [
            .. tables.Where(row => row.RollMode == LootRollMode.SinglePick && row.TotalChance > 100.0001)
        ];
    }

    public async Task<ContentEditContext<LootTable>?> LoadForEditAsync(
        Guid projectId, Guid? tableId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Loot, ct);

        if (tableId is null)
        {
            return new ContentEditContext<LootTable>
            {
                Entity = new LootTable { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var table = await db.LootTables
            .AsNoTracking()
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == tableId && t.GameProjectId == projectId, ct);

        if (table is null)
        {
            return null;
        }

        table.Entries = [.. table.Entries.OrderBy(entry => entry.SortOrder)];

        return new ContentEditContext<LootTable>
        {
            Entity = table,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, table.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, table.Id, ct)
        };
    }

    public async Task SaveTableAsync(ContentEditContext<LootTable> context, CancellationToken ct = default)
    {
        var table = context.Entity;

        if (string.IsNullOrWhiteSpace(table.Name))
        {
            throw new ContentValidationException(messages["LootNameRequired"]);
        }

        Validate(table);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.LootTables
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == table.Id, ct);

        if (stored is null)
        {
            stored = new LootTable
            {
                Id = table.Id,
                GameProjectId = table.GameProjectId,
                Name = table.Name.Trim(),
                CreatedAtUtc = now
            };

            db.LootTables.Add(stored);
        }

        stored.ContentTypeId = table.ContentTypeId;
        stored.Name = table.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(table.Description) ? null : table.Description.Trim();
        stored.RollMode = table.RollMode;
        stored.UpdatedAtUtc = now;

        SyncEntries(db, stored, table);

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        table.CreatedAtUtc = stored.CreatedAtUtc;
        table.UpdatedAtUtc = stored.UpdatedAtUtc;
        table.Name = stored.Name;
        table.Description = stored.Description;
    }

    /// <summary>
    /// Prüft nur, was in sich falsch ist. Dass die Summe bei einem Einzelwurf über 100 % liegt,
    /// wird bewusst <b>nicht</b> abgewiesen: Im Konzept steht das unter den Health Checks, also
    /// unter „nachschauen“ und nicht unter „verboten“. Sonst ließe sich eine Tabelle beim
    /// Umbauen zwischendurch nicht speichern.
    /// </summary>
    private void Validate(LootTable table)
    {
        if (table.Entries.Any(entry => entry.ItemId == Guid.Empty))
        {
            throw new ContentValidationException(messages["LootEntryItemRequired"]);
        }

        foreach (var entry in table.Entries)
        {
            if (entry.Chance is < 0 or > 100)
            {
                throw new ContentValidationException(messages["LootChanceRange"]);
            }

            if (entry.MinQuantity < 1)
            {
                throw new ContentValidationException(messages["LootMinQuantity"]);
            }

            if (entry.MaxQuantity < entry.MinQuantity)
            {
                throw new ContentValidationException(messages["LootMaxBelowMin"]);
            }
        }
    }

    private static void SyncEntries(GameDevManagerDbContext db, LootTable stored, LootTable incoming)
    {
        var wanted = incoming.Entries;
        var wantedIds = wanted.Select(entry => entry.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Entries.Where(e => !wantedIds.Contains(e.Id)).ToList())
        {
            stored.Entries.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var entry = wanted[index];
            var target = stored.Entries.FirstOrDefault(e => e.Id == entry.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet: der Eintrag bringt seine GUID schon mit, und EF
                // hielte ihn beim Anhängen an eine bestehende Tabelle sonst für einen
                // vorhandenen Datensatz — es entstünde ein UPDATE auf eine fehlende Zeile.
                db.LootEntries.Add(new LootEntry
                {
                    Id = entry.Id,
                    LootTableId = stored.Id,
                    ItemId = entry.ItemId,
                    Chance = entry.Chance,
                    MinQuantity = entry.MinQuantity,
                    MaxQuantity = entry.MaxQuantity,
                    SortOrder = index
                });
            }
            else
            {
                target.ItemId = entry.ItemId;
                target.Chance = entry.Chance;
                target.MinQuantity = entry.MinQuantity;
                target.MaxQuantity = entry.MaxQuantity;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>
    /// Löscht eine Loot-Table samt Einträgen, Feldwerten, individuellen Feldern und Sprites.
    /// NPCs, die darauf verwiesen, verlieren ihre Zuordnung.
    /// </summary>
    public async Task DeleteTableAsync(Guid tableId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(tableId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.LootTables, tableId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, tableId, ct);

        // Ohne das zeigten die NPCs auf eine Tabelle, die es nicht mehr gibt.
        await db.Npcs
            .Where(npc => npc.LootTableId == tableId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(npc => npc.LootTableId, (Guid?)null), ct);

        // Die Einträge fallen über den Fremdschlüssel mit.
        await db.LootTables
            .Where(t => t.Id == tableId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
