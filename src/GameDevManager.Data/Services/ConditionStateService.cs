using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Etwas, woran Bedingungen hängen — aufgelöst für die Zustands-Sicht.
/// </summary>
/// <param name="Label">Der Name der Entität; bei Teilobjekten der des Elternteils.</param>
/// <param name="Detail">Was am Teilobjekt hängt (Item eines Postens, Text einer Zeile) — <c>null</c> bei ganzen Entitäten.</param>
/// <param name="NavigateModuleKey">Modul der Maske, in der sich das bearbeiten lässt.</param>
/// <param name="NavigateEntityId">GUID der Maske — bei Teilobjekten der Elternteil.</param>
public sealed record ConditionedOwner(
    Guid OwnerId,
    string OwnerModuleKey,
    string Slot,
    string Label,
    string? Detail,
    string NavigateModuleKey,
    Guid NavigateEntityId,
    ConditionSet Set);

/// <summary>
/// Sammelt alle Bedingungssätze eines Projekts und löst ihre Besitzer auf — die Grundlage der
/// Zustands-Sicht (F19): Welche Quests, Dialoge, Shop-Posten und Freischaltungen wären in einem
/// angenommenen Zustand offen? Gerechnet wird nicht hier, sondern im
/// <see cref="ConditionEvaluator"/> — dieselbe Trennung wie zwischen Graph und Auswertung.
/// <para>
/// Ganze Entitäten löst die <see cref="IModuleEntitySource"/> auf. Teilobjekte (Händler-Posten,
/// Dialogzeilen und -antworten, Quest-Ziele, Spawn-Regeln) kennen die Quellen nicht — für sie
/// stehen die Nachschlagewege ausdrücklich hier, mit dem Elternteil als Sprungziel. Was danach
/// immer noch niemandem gehört, wird als „unbekannter Besitzer“ ausgewiesen statt verschwiegen.
/// </para>
/// </summary>
public class ConditionStateService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<ConditionedOwner>> GetOwnersAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sets = await db.ConditionSets
            .AsNoTracking()
            .Include(set => set.Conditions)
            .Where(set => set.GameProjectId == projectId)
            .ToListAsync(ct);

        if (sets.Count == 0)
        {
            return [];
        }

        foreach (var set in sets)
        {
            set.Conditions = [.. set.Conditions.OrderBy(condition => condition.SortOrder)];
        }

        // Ganze Entitäten: je Modul über die Quelle auflösen.
        var names = new Dictionary<Guid, string>();

        foreach (var perModule in sets.GroupBy(set => set.OwnerModuleKey))
        {
            var source = sources.FirstOrDefault(entry => entry.ModuleKey == perModule.Key);
            if (source is null)
            {
                continue;
            }

            var resolved = await source.ResolveNamesAsync(
                db, [.. perModule.Select(set => set.OwnerId).Distinct()], ct);

            foreach (var entry in resolved)
            {
                names[entry.Key] = entry.Value;
            }
        }

        var unresolved = sets
            .Select(set => set.OwnerId)
            .Where(id => !names.ContainsKey(id))
            .Distinct()
            .ToList();

        var subObjects = await ResolveSubObjectsAsync(db, unresolved, ct);
        var rows = new List<ConditionedOwner>();

        foreach (var set in sets)
        {
            if (names.TryGetValue(set.OwnerId, out var name))
            {
                rows.Add(new ConditionedOwner(
                    set.OwnerId, set.OwnerModuleKey, set.Slot,
                    name, null, set.OwnerModuleKey, set.OwnerId, set));
            }
            else if (subObjects.TryGetValue(set.OwnerId, out var sub))
            {
                rows.Add(new ConditionedOwner(
                    set.OwnerId, set.OwnerModuleKey, set.Slot,
                    sub.ParentName, sub.Detail, sub.NavigateModuleKey, sub.NavigateEntityId, set));
            }
            else
            {
                // Der Besitzer ist verschwunden (Waise) — ausweisen statt verschweigen; das
                // Sprungziel bleibt leer auf sich selbst.
                rows.Add(new ConditionedOwner(
                    set.OwnerId, set.OwnerModuleKey, set.Slot,
                    messages["ConditionOwnerUnknown"], null, set.OwnerModuleKey, set.OwnerId, set));
            }
        }

        return
        [
            .. rows
                .OrderBy(row => row.Slot)
                .ThenBy(row => row.Label)
                .ThenBy(row => row.Detail)
        ];
    }

    private sealed record SubObjectInfo(
        string ParentName, string? Detail, string NavigateModuleKey, Guid NavigateEntityId);

    /// <summary>
    /// Die bekannten Teilobjekte mit eigener GUID, an denen Bedingungen hängen. Eine
    /// Aufzählung statt einer Abstraktion: Es sind genau die Kind-Sammlungen, deren Dienste
    /// beim Löschen <c>EntityCleanup.DeleteForEntitiesAsync</c> aufrufen — kommt eine neue
    /// dazu, gehört sie auch hier hinein.
    /// </summary>
    private static async Task<Dictionary<Guid, SubObjectInfo>> ResolveSubObjectsAsync(
        GameDevManagerDbContext db, List<Guid> ids, CancellationToken ct)
    {
        var result = new Dictionary<Guid, SubObjectInfo>();

        if (ids.Count == 0)
        {
            return result;
        }

        // Händler-Posten: „Alrik — Eisenschwert“.
        foreach (var offer in await db.TraderOffers
            .AsNoTracking()
            .Where(offer => ids.Contains(offer.Id))
            .Select(offer => new
            {
                offer.Id,
                offer.NpcId,
                NpcName = offer.Npc!.Name,
                ItemName = db.Items.Where(item => item.Id == offer.ItemId).Select(item => item.Name).FirstOrDefault()
            })
            .ToListAsync(ct))
        {
            result[offer.Id] = new SubObjectInfo(
                offer.NpcName, offer.ItemName, ModuleKeys.Npcs, offer.NpcId);
        }

        // Dialogzeilen und Antworten: „Torwache — Halt!“.
        foreach (var line in await db.DialogueLines
            .AsNoTracking()
            .Where(line => ids.Contains(line.Id))
            .Select(line => new { line.Id, line.DialogueId, DialogueName = line.Dialogue!.Name, line.Text })
            .ToListAsync(ct))
        {
            result[line.Id] = new SubObjectInfo(
                line.DialogueName, line.Text, ModuleKeys.Dialogs, line.DialogueId);
        }

        foreach (var choice in await db.DialogueChoices
            .AsNoTracking()
            .Where(choice => ids.Contains(choice.Id))
            .Select(choice => new
            {
                choice.Id,
                DialogueId = choice.Line!.DialogueId,
                DialogueName = choice.Line!.Dialogue!.Name,
                choice.Text
            })
            .ToListAsync(ct))
        {
            result[choice.Id] = new SubObjectInfo(
                choice.DialogueName, choice.Text, ModuleKeys.Dialogs, choice.DialogueId);
        }

        // Quest-Ziele: „Der Aufbruch — Sammle 5 Kräuter“.
        foreach (var objective in await db.QuestObjectives
            .AsNoTracking()
            .Where(objective => ids.Contains(objective.Id))
            .Select(objective => new { objective.Id, objective.QuestId, QuestName = objective.Quest!.Name, objective.Text })
            .ToListAsync(ct))
        {
            result[objective.Id] = new SubObjectInfo(
                objective.QuestName, objective.Text, ModuleKeys.Quests, objective.QuestId);
        }

        // Spawn-Regeln: „Wolf — 2–4“.
        foreach (var rule in await db.SpawnRules
            .AsNoTracking()
            .Where(rule => ids.Contains(rule.Id))
            .Select(rule => new { rule.Id, rule.NpcId, NpcName = rule.Npc!.Name, rule.MinCount, rule.MaxCount })
            .ToListAsync(ct))
        {
            result[rule.Id] = new SubObjectInfo(
                rule.NpcName,
                rule.MinCount == rule.MaxCount ? rule.MinCount.ToString() : $"{rule.MinCount}–{rule.MaxCount}",
                ModuleKeys.Npcs,
                rule.NpcId);
        }

        return result;
    }
}
