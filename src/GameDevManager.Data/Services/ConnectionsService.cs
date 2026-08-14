using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Ein NPC im Verbindungs-Graphen samt seiner Fraktionen (in Mitglieds-Reihenfolge).</summary>
public sealed record ConnectionNode(
    Guid NpcId,
    string Name,
    NpcKind Kind,
    Guid? PrimaryAssetId,
    IReadOnlyList<Guid> FactionIds);

/// <summary>Eine Kante des Verbindungs-Graphen — gelesen „von &lt;From&gt; &lt;Label&gt; &lt;To&gt;“.</summary>
public sealed record ConnectionEdge(
    Guid FromNpcId,
    Guid ToNpcId,
    string Label,
    NpcRelationStance Stance);

/// <summary>Eine Fraktion der Legende.</summary>
public sealed record ConnectionFaction(Guid Id, string Name);

public sealed record ConnectionGraph(
    IReadOnlyList<ConnectionNode> Nodes,
    IReadOnlyList<ConnectionEdge> Edges,
    IReadOnlyList<ConnectionFaction> Factions);

/// <summary>
/// Liest den Verbindungs-Graphen: NPCs als Knoten, ihre Beziehungen als Kanten, die
/// Fraktionszugehörigkeit als Farbring. Ein Werkzeug-Modul ohne eigene Daten — dieselbe
/// Überlegung wie beim Freischaltungs-Graphen: Alles hier Gezeigte steht längst im NPC-
/// und im Fraktions-Modul, eine eigene Tabelle wäre ab der ersten Bearbeitung falsch.
/// </summary>
public class ConnectionsService(IDbContextFactory<GameDevManagerDbContext> factory)
{
    /// <param name="includeUnconnected">
    /// Auch NPCs ohne Beziehung und Fraktion zeigen — standardmäßig bleiben sie draußen,
    /// sonst bestünde der Graph bei großen Projekten vor allem aus losen Punkten.
    /// </param>
    public async Task<ConnectionGraph> GetGraphAsync(
        Guid projectId, bool includeUnconnected = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var npcs = await db.Npcs
            .AsNoTracking()
            .Where(n => n.GameProjectId == projectId)
            .OrderBy(n => n.Name).ThenBy(n => n.Id)
            .Select(n => new
            {
                n.Id,
                n.Name,
                n.Kind,
                PrimaryAssetId = db.Assets
                    .Where(a => a.OwnerEntityId == n.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var npcIds = npcs.Select(n => n.Id).ToHashSet();

        var relations = await db.NpcRelations
            .AsNoTracking()
            .Where(r => r.Npc!.GameProjectId == projectId)
            .OrderBy(r => r.Npc!.Name).ThenBy(r => r.SortOrder)
            .Select(r => new ConnectionEdge(r.NpcId, r.OtherNpcId, r.RelationType!.Name, r.Stance))
            .ToListAsync(ct);

        // Ein Ziel, das es nicht mehr gibt, fällt samt Kante heraus — dass es fehlt, zeigt
        // die Referenzansicht des Quell-NPCs.
        relations = [.. relations.Where(edge => npcIds.Contains(edge.ToNpcId))];

        var memberships = await db.FactionMembers
            .AsNoTracking()
            .Where(m => m.Faction!.GameProjectId == projectId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new { m.NpcId, m.FactionId })
            .ToListAsync(ct);

        var factionsByNpc = memberships
            .GroupBy(m => m.NpcId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.FactionId).Distinct().ToList());

        var connected = new HashSet<Guid>();
        connected.UnionWith(relations.Select(edge => edge.FromNpcId));
        connected.UnionWith(relations.Select(edge => edge.ToNpcId));
        connected.UnionWith(factionsByNpc.Keys);

        var nodes = npcs
            .Where(n => includeUnconnected || connected.Contains(n.Id))
            .Select(n => new ConnectionNode(
                n.Id,
                n.Name,
                n.Kind,
                n.PrimaryAssetId,
                factionsByNpc.GetValueOrDefault(n.Id) ?? []))
            .ToList();

        // Nur Fraktionen, die im Bild auch vorkommen — die Legende soll nicht länger sein
        // als der Graph.
        var usedFactionIds = nodes.SelectMany(node => node.FactionIds).ToHashSet();

        var factions = await db.Factions
            .AsNoTracking()
            .Where(f => f.GameProjectId == projectId)
            .OrderBy(f => f.Name).ThenBy(f => f.Id)
            .Select(f => new ConnectionFaction(f.Id, f.Name))
            .ToListAsync(ct);

        factions = [.. factions.Where(f => usedFactionIds.Contains(f.Id))];

        return new ConnectionGraph(nodes, relations, factions);
    }
}
