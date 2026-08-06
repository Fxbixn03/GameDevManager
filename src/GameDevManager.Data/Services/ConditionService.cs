using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Das einheitliche Bedingungssystem aus dem Konzept. Es hängt über GUIDs an beliebigen
/// Besitzern und funktioniert deshalb in jedem Modul gleich — Shop-Verfügbarkeit heute,
/// Dialoge und Quests später.
/// </summary>
public class ConditionService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>
    /// Lädt den Bedingungssatz eines Besitzers. Gibt es noch keinen, entsteht ein leerer —
    /// die Maske kann dann direkt daran binden.
    /// </summary>
    public async Task<ConditionSet> LoadAsync(
        Guid projectId, Guid ownerId, string ownerModuleKey, string slot, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.ConditionSets
            .AsNoTracking()
            .Include(set => set.Conditions)
            .FirstOrDefaultAsync(set => set.OwnerId == ownerId && set.Slot == slot, ct);

        if (stored is null)
        {
            return new ConditionSet
            {
                GameProjectId = projectId,
                OwnerId = ownerId,
                OwnerModuleKey = ownerModuleKey,
                Slot = slot
            };
        }

        stored.Conditions = [.. stored.Conditions.OrderBy(condition => condition.SortOrder)];
        return stored;
    }

    /// <summary>Anzahl der Bedingungen je Besitzer — für Abzeichen in Listen und Masken.</summary>
    public async Task<Dictionary<Guid, int>> CountByOwnersAsync(
        IReadOnlyCollection<Guid> ownerIds, string slot, CancellationToken ct = default)
    {
        if (ownerIds.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ConditionSets
            .AsNoTracking()
            .Where(set => ownerIds.Contains(set.OwnerId) && set.Slot == slot)
            .Select(set => new { set.OwnerId, Count = set.Conditions.Count })
            .ToDictionaryAsync(entry => entry.OwnerId, entry => entry.Count, ct);
    }

    /// <summary>
    /// Schreibt einen Bedingungssatz fort. Ein leerer Satz wird gelöscht statt gespeichert —
    /// „keine Bedingung“ soll keine Zeile in der Datenbank hinterlassen.
    /// </summary>
    public async Task SaveAsync(ConditionSet set, CancellationToken ct = default)
    {
        Validate(set);

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.ConditionSets
            .Include(existing => existing.Conditions)
            .FirstOrDefaultAsync(existing => existing.OwnerId == set.OwnerId && existing.Slot == set.Slot, ct);

        if (set.Conditions.Count == 0)
        {
            if (stored is not null)
            {
                db.ConditionSets.Remove(stored);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        if (stored is null)
        {
            stored = new ConditionSet
            {
                Id = set.Id,
                GameProjectId = set.GameProjectId,
                OwnerId = set.OwnerId,
                OwnerModuleKey = set.OwnerModuleKey,
                Slot = set.Slot
            };

            db.ConditionSets.Add(stored);
        }

        stored.Logic = set.Logic;

        var wantedIds = set.Conditions.Select(condition => condition.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Conditions.Where(c => !wantedIds.Contains(c.Id)).ToList())
        {
            stored.Conditions.Remove(obsolete);
        }

        for (var index = 0; index < set.Conditions.Count; index++)
        {
            var condition = set.Conditions[index];
            var target = stored.Conditions.FirstOrDefault(c => c.Id == condition.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet: die Bedingung bringt ihre GUID schon mit, und
                // EF hielte sie beim Anhängen an einen bestehenden Satz sonst für einen
                // vorhandenen Datensatz.
                db.Conditions.Add(Copy(condition, stored.Id, index));
            }
            else
            {
                Apply(condition, target, index);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Entfernt alle Bedingungssätze der übergebenen Besitzer.</summary>
    public static Task DeleteForOwnersAsync(
        GameDevManagerDbContext db, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Die Bedingungen fallen über den Fremdschlüssel mit.
        return db.ConditionSets
            .Where(set => ownerIds.Contains(set.OwnerId))
            .ExecuteDeleteAsync(ct);
    }

    private static void Validate(ConditionSet set)
    {
        foreach (var condition in set.Conditions)
        {
            if (condition.UsesTarget && condition.TargetEntityId is null)
            {
                throw new ContentValidationException(
                    "Diese Bedingung bezieht sich auf eine Entität — bitte eine auswählen.");
            }

            if (condition.UsesNumber && condition.NumberValue is null)
            {
                throw new ContentValidationException("Diese Bedingung braucht eine Zahl.");
            }

            if (condition.Kind == ConditionKind.Flag && string.IsNullOrWhiteSpace(condition.TextValue))
            {
                throw new ContentValidationException("Ein Schalter braucht einen Namen.");
            }

            if (condition.Kind == ConditionKind.Custom && string.IsNullOrWhiteSpace(condition.TextValue))
            {
                throw new ContentValidationException("Eine freie Bedingung braucht eine Beschreibung.");
            }
        }
    }

    private static Condition Copy(Condition source, Guid setId, int index) => new()
    {
        Id = source.Id,
        ConditionSetId = setId,
        Kind = source.Kind,
        TargetModuleKey = source.UsesTarget ? source.TargetModuleKey : null,
        TargetEntityId = source.UsesTarget ? source.TargetEntityId : null,
        Operator = source.Operator,
        NumberValue = source.UsesNumber ? source.NumberValue : null,
        BooleanValue = source.UsesBoolean ? source.BooleanValue ?? true : null,
        TextValue = string.IsNullOrWhiteSpace(source.TextValue) ? null : source.TextValue.Trim(),
        SortOrder = index
    };

    private static void Apply(Condition source, Condition target, int index)
    {
        target.Kind = source.Kind;
        target.TargetModuleKey = source.UsesTarget ? source.TargetModuleKey : null;
        target.TargetEntityId = source.UsesTarget ? source.TargetEntityId : null;
        target.Operator = source.Operator;
        target.NumberValue = source.UsesNumber ? source.NumberValue : null;
        target.BooleanValue = source.UsesBoolean ? source.BooleanValue ?? true : null;
        target.TextValue = string.IsNullOrWhiteSpace(source.TextValue) ? null : source.TextValue.Trim();
        target.SortOrder = index;
    }

    // ------------------------------------------------------------------------ Health Check

    /// <summary>
    /// Der Health Check „unerfüllbare Bedingungen“ aus dem Konzept. Gesucht wird nach zwei
    /// Dingen, die sich ohne Kenntnis des laufenden Spiels sicher feststellen lassen:
    /// <list type="bullet">
    /// <item><description>Bedingungen, deren Zielentität es nicht mehr gibt.</description></item>
    /// <item><description>
    /// Sätze mit „alle müssen zutreffen“, die sich widersprechen — etwa eine Menge, die
    /// gleichzeitig über und unter einer Grenze liegen soll, oder ein Schalter, der gesetzt
    /// und nicht gesetzt sein muss.
    /// </description></item>
    /// </list>
    /// Alles Weitere hinge vom Spielverlauf ab und wäre geraten.
    /// </summary>
    public async Task<List<ConditionProblem>> FindProblemsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sets = await db.ConditionSets
            .AsNoTracking()
            .Where(set => set.GameProjectId == projectId)
            .Include(set => set.Conditions)
            .ToListAsync(ct);

        var problems = new List<ConditionProblem>();
        var known = await LoadKnownTargetsAsync(db, projectId, sets, ct);

        foreach (var set in sets)
        {
            foreach (var condition in set.Conditions.Where(c => c.TargetEntityId is not null))
            {
                if (!known.Contains(condition.TargetEntityId!.Value))
                {
                    problems.Add(new ConditionProblem(
                        set.OwnerId,
                        set.OwnerModuleKey,
                        set.Slot,
                        "Die Bedingung bezieht sich auf eine Entität, die es nicht mehr gibt."));
                }
            }

            if (set.Logic == ConditionLogic.All)
            {
                problems.AddRange(FindContradictions(set));
            }
        }

        return problems;
    }

    /// <summary>Alle GUIDs, die es in den umgesetzten Modulen gibt — Grundlage der Ziel-Prüfung.</summary>
    private async Task<HashSet<Guid>> LoadKnownTargetsAsync(
        GameDevManagerDbContext db, Guid projectId, List<ConditionSet> sets, CancellationToken ct)
    {
        var known = new HashSet<Guid>();

        var wanted = sets
            .SelectMany(set => set.Conditions)
            .Where(condition => condition.TargetEntityId is not null)
            .Select(condition => condition.TargetModuleKey)
            .OfType<string>()
            .Distinct();

        foreach (var moduleKey in wanted)
        {
            var source = sources.FirstOrDefault(candidate => candidate.ModuleKey == moduleKey);

            // Module ohne Quelle sind noch nicht umgesetzt — deren Ziele lassen sich nicht
            // prüfen und gelten deshalb nicht als fehlend.
            if (source is null)
            {
                foreach (var id in sets.SelectMany(set => set.Conditions)
                    .Where(condition => condition.TargetModuleKey == moduleKey)
                    .Select(condition => condition.TargetEntityId!.Value))
                {
                    known.Add(id);
                }

                continue;
            }

            foreach (var entity in await source.GetEntitiesAsync(db, projectId, ct))
            {
                known.Add(entity.Id);
            }
        }

        return known;
    }

    /// <summary>Sucht Widersprüche innerhalb eines „alle müssen zutreffen“-Satzes.</summary>
    private static IEnumerable<ConditionProblem> FindContradictions(ConditionSet set)
    {
        // Ja/Nein-Bedingungen auf dieselbe Sache mit gegenläufiger Erwartung.
        var boolGroups = set.Conditions
            .Where(condition => condition.UsesBoolean)
            .GroupBy(condition => (condition.Kind, condition.TargetEntityId, condition.TextValue));

        foreach (var group in boolGroups.Where(g => g.Select(c => c.BooleanValue ?? true).Distinct().Count() > 1))
        {
            yield return new ConditionProblem(
                set.OwnerId, set.OwnerModuleKey, set.Slot,
                $"„{DescribeKind(group.Key.Kind)}“ soll gleichzeitig zutreffen und nicht zutreffen.");
        }

        // Mengenbedingungen auf dieselbe Sache, deren erlaubte Spannen sich nicht überschneiden.
        var numberGroups = set.Conditions
            .Where(condition => condition.UsesNumber && condition.NumberValue is not null)
            .GroupBy(condition => (condition.Kind, condition.TargetEntityId));

        foreach (var group in numberGroups)
        {
            var lower = double.NegativeInfinity;
            var upper = double.PositiveInfinity;

            foreach (var condition in group)
            {
                var value = condition.NumberValue!.Value;

                switch (condition.Operator)
                {
                    case ComparisonOperator.AtLeast: lower = Math.Max(lower, value); break;
                    case ComparisonOperator.GreaterThan: lower = Math.Max(lower, value + double.Epsilon); break;
                    case ComparisonOperator.AtMost: upper = Math.Min(upper, value); break;
                    case ComparisonOperator.LessThan: upper = Math.Min(upper, value - double.Epsilon); break;
                    case ComparisonOperator.Equal:
                        lower = Math.Max(lower, value);
                        upper = Math.Min(upper, value);
                        break;
                }
            }

            if (lower > upper)
            {
                yield return new ConditionProblem(
                    set.OwnerId, set.OwnerModuleKey, set.Slot,
                    $"„{DescribeKind(group.Key.Kind)}“ soll gleichzeitig mindestens {lower:0.##} "
                    + $"und höchstens {upper:0.##} sein.");
            }
        }
    }

    private static string DescribeKind(ConditionKind kind) => kind switch
    {
        ConditionKind.HasItem => "Item im Besitz",
        ConditionKind.HasCurrency => "Währung im Besitz",
        ConditionKind.QuestState => "Quest-Zustand",
        ConditionKind.NpcDefeated => "NPC besiegt",
        ConditionKind.Flag => "Schalter",
        ConditionKind.PlayerLevel => "Spielerstufe",
        _ => "Bedingung"
    };
}

/// <summary>Ein Fund des Bedingungs-Health-Checks.</summary>
public sealed record ConditionProblem(
    Guid OwnerId,
    string OwnerModuleKey,
    string Slot,
    string Message);
