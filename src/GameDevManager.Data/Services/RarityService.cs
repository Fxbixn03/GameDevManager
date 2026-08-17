using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Seltenheitsstufen. Bewusst schlanker als die übrigen
/// Modul-Dienste: Seltenheiten haben keine Arten und keine benutzerdefinierten Felder —
/// sie sind ein einfacher Nachschlagewert aus Name, Farbe und Rang.
/// </summary>
public class RarityService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Übersicht aller Seltenheiten eines Projekts, nach Rang.</summary>
    public async Task<List<RarityListRow>> GetRaritiesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Rarities
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new RarityListRow(
                r.Id,
                r.Name,
                r.Color,
                r.SortOrder,
                r.Description,
                r.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == r.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Eine neue, noch ungespeicherte Stufe — sie reiht sich hinten ein.</summary>
    public async Task<Rarity> CreateNewAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var nextRank = await db.Rarities.CountAsync(r => r.GameProjectId == projectId, ct);

        return new Rarity { GameProjectId = projectId, Name = string.Empty, SortOrder = nextRank };
    }

    public async Task<Rarity?> GetRarityAsync(Guid projectId, Guid rarityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Rarities
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rarityId && r.GameProjectId == projectId, ct);
    }

    public async Task SaveRarityAsync(Rarity rarity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rarity.Name))
        {
            throw new ContentValidationException(messages["RarityNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        // Zwei Stufen mit demselben Namen wären in keinem Auswahlfeld auseinanderzuhalten.
        var name = rarity.Name.Trim();
        var taken = await db.Rarities.AnyAsync(
            other => other.GameProjectId == rarity.GameProjectId
                && other.Name == name
                && other.Id != rarity.Id, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["RarityNameExists", name]);
        }

        var now = DateTime.UtcNow;
        var stored = await db.Rarities.FirstOrDefaultAsync(r => r.Id == rarity.Id, ct);

        if (stored is null)
        {
            stored = new Rarity
            {
                Id = rarity.Id,
                GameProjectId = rarity.GameProjectId,
                Name = name,
                CreatedAtUtc = now
            };

            db.Rarities.Add(stored);
        }

        stored.Name = name;
        stored.Color = Normalize(rarity.Color);
        stored.SortOrder = rarity.SortOrder;
        stored.Description = Normalize(rarity.Description);
        stored.Status = rarity.Status;
        stored.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);

        rarity.CreatedAtUtc = stored.CreatedAtUtc;
        rarity.UpdatedAtUtc = stored.UpdatedAtUtc;
        rarity.Name = stored.Name;
        rarity.Color = stored.Color;
        rarity.Description = stored.Description;
    }

    /// <summary>Löscht eine Seltenheit samt Sprites und eventuell verwaisten Anhängseln.</summary>
    public async Task DeleteRarityAsync(Guid rarityId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(rarityId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Rarities, rarityId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, db.Rarities, rarityId, null, ct);

        await db.Rarities
            .Where(r => r.Id == rarityId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
