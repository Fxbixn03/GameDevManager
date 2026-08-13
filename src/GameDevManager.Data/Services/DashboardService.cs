using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Eintrag beim Speichern der Anordnung: die Position ist der Index in der Liste.</summary>
public sealed record DashboardCardOrder(string CardKey, bool IsHidden);

/// <summary>
/// Sichtbarkeit und Reihenfolge der Dashboard-Cards, je Projekt. Zeilen entstehen erst beim
/// Anpassen; Cards ohne Zeile zeigt das Dashboard mit dem Standard (sichtbar, Registry-
/// Reihenfolge). Wie die Moduleinstellungen ist das Werkzeug-Konfiguration — sie steht nicht
/// im Export und übersteht den ersetzenden Import.
/// </summary>
public class DashboardService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    public async Task<Dictionary<string, DashboardCard>> GetCardsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.DashboardCards
            .AsNoTracking()
            .Where(c => c.GameProjectId == projectId)
            .ToDictionaryAsync(c => c.CardKey, ct);
    }

    public async Task SaveCardsAsync(
        Guid projectId, IReadOnlyList<DashboardCardOrder> cards, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var existing = await db.DashboardCards
            .Where(c => c.GameProjectId == projectId)
            .ToListAsync(ct);
        var byKey = existing.ToDictionary(c => c.CardKey);
        var seen = new HashSet<string>();

        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            seen.Add(card.CardKey);

            if (byKey.TryGetValue(card.CardKey, out var row))
            {
                row.IsHidden = card.IsHidden;
                row.SortOrder = i;
            }
            else
            {
                db.DashboardCards.Add(new DashboardCard
                {
                    GameProjectId = projectId,
                    CardKey = card.CardKey,
                    IsHidden = card.IsHidden,
                    SortOrder = i
                });
            }
        }

        // Cards, die nicht mehr dabei sind (Modul abgeschaltet): Zeile weg — kommt das Modul
        // zurück, gilt wieder der Standard.
        db.DashboardCards.RemoveRange(existing.Where(c => !seen.Contains(c.CardKey)));

        await db.SaveChangesAsync(ct);
    }
}
