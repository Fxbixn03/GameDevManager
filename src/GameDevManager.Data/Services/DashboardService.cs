using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Eintrag beim Speichern der Anordnung: die Position ist der Index in der Liste.</summary>
public sealed record DashboardBandOrder(string BandKey, bool IsHidden);

/// <summary>Ein Band in seiner aufgelösten Reihenfolge — Vorgabe und gespeicherte Zeile verrechnet.</summary>
public sealed record DashboardBandState(string BandKey, bool IsVisible);

/// <summary>
/// Sichtbarkeit und Reihenfolge der Dashboard-Bänder, je Projekt. Zeilen entstehen erst beim
/// Anpassen; Bänder ohne Zeile zeigt das Dashboard mit dem Standard aus
/// <see cref="DashboardBands"/>. Wie die Moduleinstellungen ist das Werkzeug-Konfiguration —
/// sie steht nicht im Export und übersteht den ersetzenden Import.
/// </summary>
public class DashboardService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>
    /// Alle Bänder in ihrer Reihenfolge, sichtbare wie ausgeblendete — die Anzeige filtert
    /// selbst, der Anpassen-Dialog braucht beide. Angepasste Bänder sortieren sich über ihren
    /// gespeicherten Platz ein, alle übrigen behalten die Vorgabe-Reihenfolge dahinter.
    /// </summary>
    public async Task<List<DashboardBandState>> GetBandsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Unbekannte Schlüssel werden hier aussortiert: in Bestandsprojekten stehen in dieser
        // Tabelle noch die Modul-Karten des alten Dashboards.
        var stored = await db.DashboardCards
            .AsNoTracking()
            .Where(card => card.GameProjectId == projectId)
            .ToDictionaryAsync(card => card.CardKey, ct);

        return
        [
            .. DashboardBands.All
                .Select((key, defaultPosition) => (Key: key, DefaultPosition: defaultPosition))
                .OrderBy(band => stored.TryGetValue(band.Key, out var card) ? card.SortOrder : int.MaxValue)
                .ThenBy(band => band.DefaultPosition)
                .Select(band => new DashboardBandState(
                    band.Key,
                    stored.TryGetValue(band.Key, out var card)
                        ? !card.IsHidden
                        : !DashboardBands.IsHiddenByDefault(band.Key)))
        ];
    }

    public async Task SaveBandsAsync(
        Guid projectId, IReadOnlyList<DashboardBandOrder> bands, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var existing = await db.DashboardCards
            .Where(card => card.GameProjectId == projectId)
            .ToListAsync(ct);
        var byKey = existing.ToDictionary(card => card.CardKey);
        var seen = new HashSet<string>();

        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            seen.Add(band.BandKey);

            if (byKey.TryGetValue(band.BandKey, out var row))
            {
                row.IsHidden = band.IsHidden;
                row.SortOrder = i;
            }
            else
            {
                db.DashboardCards.Add(new DashboardCard
                {
                    GameProjectId = projectId,
                    CardKey = band.BandKey,
                    IsHidden = band.IsHidden,
                    SortOrder = i
                });
            }
        }

        // Alles, was nicht mitgeschickt wurde, verliert seine Zeile — damit räumt das erste
        // Speichern zugleich die Modul-Karten des alten Dashboards ab.
        db.DashboardCards.RemoveRange(existing.Where(card => !seen.Contains(card.CardKey)));

        await db.SaveChangesAsync(ct);
    }
}
