using GameDevManager.Domain;
using GameDevManager.Domain.Curves;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lädt die Zutaten des Kampf-Simulators: die Feld-Zuordnung des Projekts und die Werte
/// eines NPCs, auf die vier Rollen heruntergerechnet. Spielerfiguren sind seit dem
/// Spieler-Umbau überführbare NPCs — „Spieler gegen Boss“ ist damit derselbe Fall.
/// </summary>
public class CombatService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <summary>Die Zuordnung des Projekts — eine leere, wenn noch keine gespeichert ist.</summary>
    public async Task<CombatMapping> GetMappingAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.CombatMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(mapping => mapping.GameProjectId == projectId, ct)
            ?? new CombatMapping { GameProjectId = projectId };
    }

    /// <summary>
    /// Speichert die Zuordnung. Eine Zeile je Projekt; der Schreibschutz greift am
    /// <c>SaveChanges</c> von selbst.
    /// </summary>
    public async Task SaveMappingAsync(CombatMapping mapping, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.CombatMappings
            .FirstOrDefaultAsync(existing => existing.GameProjectId == mapping.GameProjectId, ct);

        if (stored is null)
        {
            db.CombatMappings.Add(new CombatMapping
            {
                GameProjectId = mapping.GameProjectId,
                HealthFieldId = mapping.HealthFieldId,
                DamageFieldId = mapping.DamageFieldId,
                DefenseFieldId = mapping.DefenseFieldId,
                SpeedFieldId = mapping.SpeedFieldId
            });
        }
        else
        {
            stored.HealthFieldId = mapping.HealthFieldId;
            stored.DamageFieldId = mapping.DamageFieldId;
            stored.DefenseFieldId = mapping.DefenseFieldId;
            stored.SpeedFieldId = mapping.SpeedFieldId;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Die Werte eines NPCs, auf die vier Rollen heruntergerechnet — <c>null</c>, wenn es
    /// ihn nicht (mehr) gibt. Ein Zahlenfeld liefert seine Zahl; ein <b>Kurvenfeld</b> wird
    /// auf der übergebenen Stufe ausgewertet — genau der Fall „Spieler-Kurvenwerte auf
    /// Stufe X“. Ein fehlender Wert zählt als 0 und wird nicht erfunden; ob eine Rolle
    /// überhaupt zugeordnet ist, prüft die Oberfläche vor dem Lauf.
    /// </summary>
    public async Task<CombatantStats?> ResolveStatsAsync(
        Guid projectId, Guid npcId, CombatMapping mapping, double level, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var npc = await db.Npcs
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == npcId && n.GameProjectId == projectId, ct);

        if (npc is null)
        {
            return null;
        }

        // Aufgelöst inklusive Varianten-Vererbung — dieselbe Strecke wie die Maske.
        var values = await ContentFields.LoadValuesAsync<Npc>(db, npcId, ct);

        double Stat(Guid? fieldId) => Resolve(values, fieldId, level);

        return new CombatantStats(
            npc.Name,
            Stat(mapping.HealthFieldId),
            Stat(mapping.DamageFieldId),
            Stat(mapping.DefenseFieldId),
            Stat(mapping.SpeedFieldId));
    }

    private static double Resolve(
        IReadOnlyDictionary<Guid, FieldValue> values, Guid? fieldId, double level)
    {
        if (fieldId is not { } id || !values.TryGetValue(id, out var value))
        {
            return 0;
        }

        if (value.NumberValue is { } number)
        {
            return number;
        }

        // Ein Kurvenfeld trägt sein JSON im Text — ausgewertet auf der gewählten Stufe.
        // Text, der keine Kurve ist, ergibt beim Lesen null und damit 0 — dieselbe
        // Zurückhaltung wie beim Feldtyp selbst.
        return CurveDefinition.Parse(value.TextValue)?.ValueAt(level) ?? 0;
    }
}
