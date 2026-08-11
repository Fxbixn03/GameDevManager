using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Das Statistik-Modul: Kennzahlen über alle Module und die Health Checks, die keinen
/// eigenen Platz in einem Fachmodul haben — „toter Content“ und verwaiste Sprites.
/// Die übrigen Health Checks des Konzepts liegen bei ihren Modulen (Crafting-Zyklen,
/// Loot über 100 %, Dialog-Sackgassen, Quests ohne Abschluss, unerfüllbare Bedingungen)
/// und werden auf der Statistik-Seite nur zusammengeführt.
/// </summary>
public class StatisticsService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>Anzahl der Entitäten je Modul — die Kennzahlen des Konzepts.</summary>
    public async Task<List<ModuleCount>> GetCountsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Bewusst je Modul eine eigene Zählung statt über die Quellen zu laden — die
        // Übersicht braucht nur Zahlen, keine Entitätenlisten.
        var counts = new List<ModuleCount>
        {
            new(ModuleKeys.Items, await db.Items.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Crafting, await db.Recipes.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Currencies, await db.Currencies.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Npcs, await db.Npcs.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Factions, await db.Factions.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Diplomacy, await db.DiplomaticRelations.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Maps, await db.Maps.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Dialogs, await db.Dialogues.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Story, await db.StoryEntries.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Quests, await db.Quests.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Events, await db.GameEvents.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Player, await db.Skills.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Classes, await db.CharacterClasses.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Loot, await db.LootTables.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Effects, await db.GameEffects.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Achievements, await db.Achievements.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Collectibles, await db.Collectibles.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Audio, await db.SoundEffects.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Cutscenes, await db.Cutscenes.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Tags, await db.ContentTags.CountAsync(e => e.GameProjectId == projectId, ct)),
            new(ModuleKeys.Assets, await db.Assets.CountAsync(e => e.GameProjectId == projectId, ct))
        };

        return counts;
    }

    /// <summary>Die Detailzahlen der NPC-Frage des Konzepts: „wie viele NPCs feindlich sind“.</summary>
    public async Task<NpcBreakdown> GetNpcBreakdownAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return new NpcBreakdown(
            await db.Npcs.CountAsync(n => n.GameProjectId == projectId && n.Kind == NpcKind.Npc, ct),
            await db.Npcs.CountAsync(n => n.GameProjectId == projectId && n.Kind == NpcKind.Mob, ct),
            await db.Npcs.CountAsync(n => n.GameProjectId == projectId && n.IsTrader, ct),
            await db.Npcs.CountAsync(n => n.GameProjectId == projectId && n.IsQuestGiver, ct));
    }

    /// <summary>
    /// Der Health Check „Items ohne jede Bezugsquelle“ aus dem Konzept: Items, die kein
    /// Rezept herstellt, kein Händler führt und keine Loot-Table fallen lässt —
    /// toter Content, an den der Spieler nie herankommt.
    /// </summary>
    public async Task<List<EntitySummary>> FindDeadItemsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Items
            .AsNoTracking()
            .Where(item => item.GameProjectId == projectId
                && !db.RecipeOutputs.Any(output => output.ItemId == item.Id)
                && !db.TraderOffers.Any(offer => offer.ItemId == item.Id)
                && !db.LootEntries.Any(entry => entry.ItemId == item.Id))
            .OrderBy(item => item.Name)
            .Select(item => new EntitySummary(item.Id, ModuleKeys.Items, item.Name, item.ContentType!.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Der Health Check „verwaiste Sprites“ aus dem Konzept: Assets, deren Besitzer-GUID in
    /// keinem Modul mehr auflösbar ist. Werkzeug-Assets ohne Besitzer sind kein Fund —
    /// sie gehören bewusst niemandem.
    /// </summary>
    public async Task<List<OrphanedAsset>> FindOrphanedAssetsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var owned = await db.Assets
            .AsNoTracking()
            .Where(a => a.GameProjectId == projectId && a.OwnerEntityId != null)
            .Select(a => new { a.Id, a.FileName, a.OwnerEntityId, a.OwnerModuleKey })
            .ToListAsync(ct);

        var orphans = new List<OrphanedAsset>();

        foreach (var perModule in owned.GroupBy(a => a.OwnerModuleKey))
        {
            var ids = perModule.Select(a => a.OwnerEntityId!.Value).Distinct().ToList();
            var known = new HashSet<Guid>();

            var source = sources.FirstOrDefault(s => s.ModuleKey == perModule.Key);
            if (source is not null)
            {
                var resolved = await source.ResolveNamesAsync(db, ids, ct);
                known.UnionWith(resolved.Keys);
            }

            // Die Spieler-Quelle kennt nur Skills — die Spielerfiguren hängen ihre Sprites
            // aber an dasselbe Modul und dürfen nicht als verwaist gelten.
            if (perModule.Key == ModuleKeys.Player)
            {
                known.UnionWith(await db.PlayerCharacters
                    .Where(p => ids.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync(ct));
            }

            if (source is null && perModule.Key != ModuleKeys.Player)
            {
                // Modul ohne Quelle — Verwaisung ist hier nicht sicher feststellbar, kein Fund.
                continue;
            }

            orphans.AddRange(perModule
                .Where(a => !known.Contains(a.OwnerEntityId!.Value))
                .Select(a => new OrphanedAsset(a.Id, a.FileName, a.OwnerModuleKey)));
        }

        return [.. orphans.OrderBy(o => o.FileName)];
    }
}

/// <summary>Anzahl der Entitäten eines Moduls.</summary>
public sealed record ModuleCount(string ModuleKey, int Count);

/// <summary>Die NPC-Kennzahlen: friedlich/feindlich und die Rollen.</summary>
public sealed record NpcBreakdown(int NpcCount, int MobCount, int TraderCount, int QuestGiverCount);

/// <summary>Ein Sprite, dessen Besitzer es nicht mehr gibt.</summary>
public sealed record OrphanedAsset(Guid AssetId, string FileName, string? OwnerModuleKey);
