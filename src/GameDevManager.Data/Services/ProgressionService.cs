using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Ein Inhalt, der auf einer bestimmten Stufe dazukommt.</summary>
/// <param name="Level">
/// Die Stufe, ab der er erreichbar ist — <c>null</c> heißt „jederzeit“: nichts auf seinem Weg
/// verlangt eine Stufe.
/// </param>
/// <param name="IsInherited">
/// Die Stufe steht nicht an ihm selbst, sondern an etwas, das er voraussetzt. Ein Skill, der
/// einen Skill von Stufe 10 braucht, ist selbst frühestens auf Stufe 10 zu haben.
/// </param>
public sealed record ProgressionEntry(
    Guid EntityId,
    string ModuleKey,
    string Name,
    int? Level,
    bool IsInherited);

/// <summary>
/// Die Fortschritts-Sicht: was bekommt der Spieler auf welcher Stufe?
/// <para>
/// Der Freischaltungs-Graph zeigt, <b>woran</b> etwas hängt; diese Auswertung zeigt,
/// <b>wann</b> es kommt. Die Grundlage ist dieselbe — <see cref="ConditionKind.PlayerLevel"/>
/// in den Slots „wird freigeschaltet, wenn …“ und „ist verfügbar, wenn …“ — und wie dort gibt
/// es keinen eigenen Datenbestand.
/// </para>
/// <para>
/// <b>Die Stufe erbt sich über den Graphen.</b> Was einen Skill von Stufe 10 voraussetzt, ist
/// selbst frühestens auf Stufe 10 zu haben, auch wenn an ihm selbst keine Stufe steht — sonst
/// stünde die halbe Kette in der Spalte „jederzeit“ und die Zeitleiste sagte nichts.
/// Genommen wird dabei die <b>höchste</b> Stufe auf dem Weg: Wer zwei Voraussetzungen hat,
/// wartet auf die spätere.
/// </para>
/// </summary>
public class ProgressionService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    TechTreeService techTree)
{
    /// <summary>
    /// Alles Freischaltbare nach Stufe. Einträge ohne Stufenbezug tragen <c>null</c> und
    /// landen in der Spalte „jederzeit“.
    /// </summary>
    public async Task<List<ProgressionEntry>> GetProgressionAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var graph = await techTree.GetGraphAsync(projectId, ct);
        if (graph.Nodes.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        // Die Stufen, die unmittelbar an einer Entität hängen. Eine Entität kann mehrere
        // Bedingungssätze haben; gefordert ist dann die höchste.
        var direct = new Dictionary<Guid, int>();

        // Geladen wird über Include und gefiltert im Speicher: Ein Where innerhalb von
        // SelectMany über eine Navigationsliste verlangt SQL APPLY, und das kennt SQLite nicht.
        // Denselben Weg geht der TechTreeService.
        var sets = await db.ConditionSets
            .AsNoTracking()
            .Where(set => set.GameProjectId == projectId)
            .Include(set => set.Conditions)
            .ToListAsync(ct);

        foreach (var set in sets)
        {
            foreach (var condition in set.Conditions)
            {
                if (condition.Kind != ConditionKind.PlayerLevel || condition.NumberValue is not { } value)
                {
                    continue;
                }

                var level = (int)Math.Round(value);

                direct[set.OwnerId] = direct.TryGetValue(set.OwnerId, out var known)
                    ? Math.Max(known, level)
                    : level;
            }
        }

        // Kanten nach Ziel: „was muss vorher da sein?“ — die Richtung, in der geerbt wird.
        var requirements = graph.Edges
            .GroupBy(edge => edge.ToEntityId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.FromEntityId).ToList());

        var resolved = new Dictionary<Guid, int?>();
        var entries = new List<ProgressionEntry>();

        foreach (var node in graph.Nodes)
        {
            var level = Resolve(node.EntityId, direct, requirements, resolved, []);

            entries.Add(new ProgressionEntry(
                node.EntityId,
                node.ModuleKey,
                node.Name,
                level,
                level is not null && !direct.ContainsKey(node.EntityId)));
        }

        return
        [
            .. entries
                .OrderBy(entry => entry.Level ?? int.MaxValue)
                .ThenBy(entry => entry.ModuleKey)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    /// <summary>
    /// Die Stufe einer Entität: die eigene, sonst die höchste ihrer Voraussetzungen.
    /// <para>
    /// <paramref name="path"/> bricht Ringe ab. Der Health Check meldet sie ohnehin; hier
    /// dürfen sie nur nicht dazu führen, dass die Auswertung endlos läuft.
    /// </para>
    /// </summary>
    private static int? Resolve(
        Guid entityId,
        Dictionary<Guid, int> direct,
        Dictionary<Guid, List<Guid>> requirements,
        Dictionary<Guid, int?> resolved,
        HashSet<Guid> path)
    {
        if (resolved.TryGetValue(entityId, out var cached))
        {
            return cached;
        }

        if (!path.Add(entityId))
        {
            return null;
        }

        int? level = direct.TryGetValue(entityId, out var own) ? own : null;

        if (requirements.TryGetValue(entityId, out var sources))
        {
            foreach (var source in sources)
            {
                var inherited = Resolve(source, direct, requirements, resolved, path);

                if (inherited is { } value && (level is null || value > level))
                {
                    level = value;
                }
            }
        }

        path.Remove(entityId);

        // Nur außerhalb eines Ringes merken: Ein Zwischenergebnis aus einem abgebrochenen
        // Pfad wäre für andere Wege falsch.
        if (path.Count == 0)
        {
            resolved[entityId] = level;
        }

        return level;
    }
}
