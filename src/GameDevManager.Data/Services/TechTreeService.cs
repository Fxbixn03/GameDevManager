using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Ein Knoten des Freischaltungs-Graphen: irgendetwas, das freigeschaltet wird oder etwas
/// freischaltet. <paramref name="Depth"/> ist der Abstand vom Anfang — Knoten ohne
/// Voraussetzung liegen auf 0, ihre Nachfolger darüber hinaus.
/// </summary>
public sealed record UnlockNode(
    Guid EntityId,
    string ModuleKey,
    string Name,
    int Depth,
    int RequirementCount,
    bool IsInCycle)
{
    /// <summary>Ganz am Anfang: nichts muss vorher freigeschaltet sein.</summary>
    public bool IsRoot => RequirementCount == 0;
}

/// <summary>
/// Eine Kante: <paramref name="FromEntityId"/> muss freigeschaltet sein, damit
/// <paramref name="ToEntityId"/> es werden kann.
/// </summary>
/// <param name="Slot">
/// Aus welchem Bedingungssatz die Kante stammt — „wird freigeschaltet, wenn …“ oder
/// „ist verfügbar, wenn …“. Die Ansicht unterscheidet beides in der Zeichnung.
/// </param>
/// <param name="IsOptional">
/// Der Satz ist ein „mindestens eine trifft zu“. Dann ist diese Voraussetzung ein Weg von
/// mehreren und keine Pflicht — sonst läse man aus dem Bild eine Kette, die keine ist.
/// </param>
public sealed record UnlockEdge(
    Guid FromEntityId,
    Guid ToEntityId,
    string Slot,
    bool IsOptional);

/// <summary>Ein Ring im Freischaltungs-Graphen: jeder wartet auf den nächsten.</summary>
public sealed record UnlockCycle(IReadOnlyList<UnlockNode> Nodes);

/// <summary>Der komplette Freischaltungs-Graph eines Projekts.</summary>
public sealed record UnlockGraph(
    IReadOnlyList<UnlockNode> Nodes,
    IReadOnlyList<UnlockEdge> Edges,
    IReadOnlyList<UnlockCycle> Cycles)
{
    public int MaxDepth => Nodes.Count == 0 ? 0 : Nodes.Max(node => node.Depth);
}

/// <summary>
/// Der Tech-Tree aus dem Konzept — „Tech-Tree/Freischaltungen“ unter „Weiteres inhaltlich“.
/// <para>
/// Er bringt <b>keine eigenen Daten</b> mit. Was etwas freischaltet, steht längst im
/// Bedingungssystem: ein Bedingungssatz im Slot „wird freigeschaltet, wenn …“ oder
/// „ist verfügbar, wenn …“, dessen Bedingungen auf andere Entitäten zeigen. Dieser Dienst
/// liest genau das als gerichteten Graphen — Voraussetzung zeigt auf Freigeschaltetes.
/// </para>
/// <para>
/// Ein eigenes Modul mit eigener Tabelle hätte dieselbe Aussage ein zweites Mal gespeichert
/// und wäre ab der ersten Bearbeitung im Bedingungs-Editor falsch. Deshalb ist der Tech-Tree
/// eine Ansicht und kein Inhalt — wie der Diplomatie- und der Dialog-Graph.
/// </para>
/// </summary>
public class TechTreeService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Die Bedingungssätze, aus denen Freischaltungen gelesen werden. „Wird freigeschaltet,
    /// wenn …“ ist der ausdrückliche Fall; „ist verfügbar, wenn …“ ist derselbe Gedanke für
    /// Module, die keinen eigenen Freischalt-Slot haben.
    /// </summary>
    private static readonly string[] UnlockSlots = [ConditionSlots.Unlock, ConditionSlots.Availability];

    /// <summary>
    /// Baut den Graphen. Geladen wird der gesamte Bedingungsbestand eines Projekts einmal und
    /// im Speicher aufgelöst — dieselbe Überlegung wie beim Crafting-Graphen: In der
    /// Größenordnung eines Spielprojekts ist das deutlich billiger als eine Abfrage je Ebene.
    /// </summary>
    public async Task<UnlockGraph> GetGraphAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sets = await db.ConditionSets
            .AsNoTracking()
            .Where(set => set.GameProjectId == projectId && UnlockSlots.Contains(set.Slot))
            .Include(set => set.Conditions)
            .ToListAsync(ct);

        // Nur Bedingungen, die auf eine andere Entität zeigen, sind eine Freischaltung.
        // „Spieler hat Stufe 20“ ist eine Voraussetzung, aber kein Knoten im Baum.
        var edges = new List<UnlockEdge>();
        var wanted = new Dictionary<Guid, string>();

        foreach (var set in sets)
        {
            RememberModule(wanted, set.OwnerId, set.OwnerModuleKey);

            foreach (var condition in set.Conditions)
            {
                // Ein „darf nicht freigeschaltet sein“ ist eine Sperre und keine Voraussetzung —
                // als Kante gelesen zeigte der Baum das Gegenteil dessen, was dasteht.
                if (condition.TargetEntityId is not { } targetId
                    || condition.TargetModule is not { } targetModule
                    || condition.BooleanValue == false)
                {
                    continue;
                }

                RememberModule(wanted, targetId, targetModule);

                edges.Add(new UnlockEdge(
                    targetId,
                    set.OwnerId,
                    set.Slot,
                    set.Logic == ConditionLogic.Any));
            }
        }

        if (wanted.Count == 0)
        {
            return new UnlockGraph([], [], []);
        }

        var names = await ResolveNamesAsync(db, projectId, wanted, ct);

        // Was sich nicht mehr auflösen lässt, fliegt samt seiner Kanten heraus: Ein Knoten
        // ohne Namen wäre im Bild eine leere Kachel, und dass es das Ziel nicht mehr gibt,
        // meldet bereits der Health Check „unerfüllbare Bedingungen“.
        edges = [.. edges.Where(edge => names.ContainsKey(edge.FromEntityId) && names.ContainsKey(edge.ToEntityId))];

        var requirementCounts = edges
            .GroupBy(edge => edge.ToEntityId)
            .ToDictionary(group => group.Key, group => group.Count());

        var depths = ComputeDepths(names.Keys, edges, out var cyclic);

        var nodes = names
            .Select(pair => new UnlockNode(
                pair.Key,
                wanted[pair.Key],
                pair.Value,
                depths[pair.Key],
                requirementCounts.GetValueOrDefault(pair.Key),
                cyclic.Contains(pair.Key)))
            .OrderBy(node => node.Depth)
            .ThenBy(node => node.Name)
            .ToList();

        return new UnlockGraph(nodes, edges, FindCycles(nodes, edges, cyclic));
    }

    /// <summary>
    /// Der Health Check zum Freischaltungs-Graphen: Ringe, in denen jeder auf den nächsten
    /// wartet. Nichts davon lässt sich je erreichen — im Spiel bliebe der ganze Ring aus.
    /// <para>
    /// Derselbe Fall wie die zyklischen Rezepte des Konzepts, nur eine Ebene höher.
    /// </para>
    /// </summary>
    public async Task<List<UnlockCycle>> FindCyclesAsync(Guid projectId, CancellationToken ct = default) =>
        [.. (await GetGraphAsync(projectId, ct)).Cycles];

    /// <summary>
    /// Die Namen aller beteiligten Entitäten, je Modul über dessen Quelle. Module ohne Quelle
    /// (noch nicht umgesetzte) liefern nichts und fallen damit aus dem Graphen.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, Guid projectId, Dictionary<Guid, string> wanted, CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var perModule in wanted.GroupBy(pair => pair.Value))
        {
            var ids = perModule.Select(pair => pair.Key).ToList();
            var source = sources.FirstOrDefault(candidate => candidate.ModuleKey == perModule.Key);

            if (source is not null)
            {
                foreach (var (id, name) in await source.ResolveNamesAsync(db, ids, ct))
                {
                    names[id] = name;
                }

                continue;
            }

            // Die Spieler-Quelle kennt nur Skills; die Spielerfiguren hängen am selben Modul.
            if (perModule.Key == ModuleKeys.Player)
            {
                foreach (var character in await db.PlayerCharacters
                             .AsNoTracking()
                             .Where(p => ids.Contains(p.Id) && p.GameProjectId == projectId)
                             .Select(p => new { p.Id, p.Name })
                             .ToListAsync(ct))
                {
                    names[character.Id] = character.Name;
                }
            }
        }

        return names;
    }

    private static void RememberModule(Dictionary<Guid, string> wanted, Guid entityId, string moduleKey)
    {
        if (entityId != Guid.Empty)
        {
            wanted[entityId] = moduleKey;
        }
    }

    /// <summary>
    /// Die Tiefe jedes Knotens: der längste Weg von einem Knoten ohne Voraussetzung her.
    /// Berechnet über eine topologische Sortierung nach Kahn — was danach übrig bleibt, steckt
    /// in einem Ring und bekommt die Tiefe seiner tiefsten erreichten Voraussetzung, damit es
    /// im Bild nicht ganz vorne landet.
    /// </summary>
    private static Dictionary<Guid, int> ComputeDepths(
        IEnumerable<Guid> allNodes, List<UnlockEdge> edges, out HashSet<Guid> cyclic)
    {
        var depths = allNodes.ToDictionary(id => id, _ => 0);
        var outgoing = edges
            .GroupBy(edge => edge.FromEntityId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToEntityId).ToList());

        var remaining = depths.Keys.ToDictionary(
            id => id,
            id => edges.Count(edge => edge.ToEntityId == id));

        var ready = new Queue<Guid>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var settled = new HashSet<Guid>();

        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            settled.Add(current);

            foreach (var next in outgoing.GetValueOrDefault(current, []))
            {
                depths[next] = Math.Max(depths[next], depths[current] + 1);

                if (--remaining[next] == 0)
                {
                    ready.Enqueue(next);
                }
            }
        }

        cyclic = [.. depths.Keys.Where(id => !settled.Contains(id))];
        return depths;
    }

    /// <summary>
    /// Zerlegt die Ringe in einzelne Zyklen. Gesucht wird je unaufgelöstem Knoten der Weg
    /// zurück zu ihm selbst — mehr braucht die Meldung nicht: Wer den Ring sieht, sieht auch,
    /// welche Bedingung zu viel ist.
    /// </summary>
    private static List<UnlockCycle> FindCycles(
        List<UnlockNode> nodes, List<UnlockEdge> edges, HashSet<Guid> cyclic)
    {
        if (cyclic.Count == 0)
        {
            return [];
        }

        var byId = nodes.ToDictionary(node => node.EntityId);
        var outgoing = edges
            .Where(edge => cyclic.Contains(edge.FromEntityId) && cyclic.Contains(edge.ToEntityId))
            .GroupBy(edge => edge.FromEntityId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToEntityId).Distinct().ToList());

        var cycles = new List<UnlockCycle>();
        var reported = new HashSet<string>();

        foreach (var start in cyclic.OrderBy(id => byId.GetValueOrDefault(id)?.Name, StringComparer.Ordinal))
        {
            var path = new List<Guid>();
            var visiting = new HashSet<Guid>();

            if (!Walk(start, start, outgoing, path, visiting))
            {
                continue;
            }

            // Denselben Ring von einem anderen Knoten aus gefunden — einmal genügt.
            var signature = string.Join(">", path.OrderBy(id => id));

            if (reported.Add(signature))
            {
                cycles.Add(new UnlockCycle([.. path.Select(id => byId[id])]));
            }
        }

        return cycles;
    }

    private static bool Walk(
        Guid current, Guid target, Dictionary<Guid, List<Guid>> outgoing,
        List<Guid> path, HashSet<Guid> visiting)
    {
        path.Add(current);
        visiting.Add(current);

        foreach (var next in outgoing.GetValueOrDefault(current, []))
        {
            if (next == target || (!visiting.Contains(next) && Walk(next, target, outgoing, path, visiting)))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(current);
        return false;
    }

    /// <summary>
    /// Beschreibt einen Ring als Satz — „A → B → A“. Der Text kommt aus den DataMessages,
    /// weil die Statistik-Seite ihn unverändert anzeigt.
    /// </summary>
    public string DescribeCycle(UnlockCycle cycle) =>
        messages["UnlockCycle", string.Join(" → ", cycle.Nodes.Select(node => node.Name))];
}
