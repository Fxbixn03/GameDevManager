using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Items für die modulübergreifenden Dienste.
/// </summary>
public sealed class ItemEntitySource : ModuleEntitySource<Item>
{
    public override string ModuleKey => ModuleKeys.Items;

    protected override DbSet<Item> Set(GameDevManagerDbContext db) => db.Items;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Item> query) =>
        query.Select(item => new SearchHit(
            item.Id,
            ModuleKeys.Items,
            SearchHitKind.Entity,
            item.Name,
            item.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == item.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
}

/// <summary>
/// Rezepte für die modulübergreifenden Dienste. Als einziges Modul verweist es bisher über
/// eigene Spalten auf fremde Entitäten — Ergebnis und Zutaten zeigen auf Items.
/// </summary>
public sealed class RecipeEntitySource : ModuleEntitySource<Recipe>
{
    public override string ModuleKey => ModuleKeys.Crafting;

    protected override DbSet<Recipe> Set(GameDevManagerDbContext db) => db.Recipes;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Recipe> query) =>
        query.Select(recipe => new SearchHit(
            recipe.Id,
            ModuleKeys.Crafting,
            SearchHitKind.Entity,
            recipe.Name,
            db.Items.Where(i => i.Id == recipe.OutputItemId).Select(i => "ergibt " + i.Name).FirstOrDefault(),
            db.Assets.Where(a => a.OwnerEntityId == recipe.OutputItemId && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.OutputItemId == entityId)
            .Select(recipe => new EntityReferenceHit(recipe.Id, ModuleKeys.Crafting, recipe.Name, "Ergebnis"))
            .ToListAsync(ct);

        hits.AddRange(await db.RecipeIngredients
            .AsNoTracking()
            .Where(ingredient => ingredient.ItemId == entityId)
            .Select(ingredient => new EntityReferenceHit(
                ingredient.RecipeId, ModuleKeys.Crafting, ingredient.Recipe!.Name, "Zutat"))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// NPCs und Mobs für die modulübergreifenden Dienste. Ihre Warenangebote verweisen über
/// eigene Spalten auf Items und Währungen.
/// </summary>
public sealed class NpcEntitySource : ModuleEntitySource<Npc>
{
    public override string ModuleKey => ModuleKeys.Npcs;

    protected override DbSet<Npc> Set(GameDevManagerDbContext db) => db.Npcs;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Npc> query) =>
        query.Select(npc => new SearchHit(
            npc.Id,
            ModuleKeys.Npcs,
            SearchHitKind.Entity,
            npc.Name,
            npc.Kind == NpcKind.Mob ? "Mob" : npc.IsTrader ? "Händler" : npc.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == npc.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.ItemId == entityId)
            .Select(offer => new EntityReferenceHit(
                offer.NpcId, ModuleKeys.Npcs, offer.Npc!.Name, "Handelsware"))
            .ToListAsync(ct);

        hits.AddRange(await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.CurrencyId == entityId)
            .Select(offer => new EntityReferenceHit(
                offer.NpcId, ModuleKeys.Npcs, offer.Npc!.Name, "Währung im Angebot"))
            .Distinct()
            .ToListAsync(ct));

        hits.AddRange(await db.Npcs
            .AsNoTracking()
            .Where(npc => npc.LootTableId == entityId)
            .Select(npc => new EntityReferenceHit(npc.Id, ModuleKeys.Npcs, npc.Name, "Loot-Table"))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Dialoge für die modulübergreifenden Dienste. Beteiligte und Sprecher verweisen über
/// eigene Spalten auf NPCs.
/// </summary>
public sealed class DialogueEntitySource : ModuleEntitySource<Dialogue>
{
    public override string ModuleKey => ModuleKeys.Dialogs;

    protected override DbSet<Dialogue> Set(GameDevManagerDbContext db) => db.Dialogues;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Dialogue> query) =>
        query.Select(dialogue => new SearchHit(
            dialogue.Id,
            ModuleKeys.Dialogs,
            SearchHitKind.Entity,
            dialogue.Name,
            dialogue.Kind == DialogueKind.Bark ? "Sprechblasen" : dialogue.Lines.Count + " Zeile(n)",
            null));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.DialogueParticipants
            .AsNoTracking()
            .Where(participant => participant.NpcId == entityId)
            .Select(participant => new EntityReferenceHit(
                participant.DialogueId, ModuleKeys.Dialogs, participant.Dialogue!.Name, "Beteiligt"))
            .ToListAsync(ct);

        hits.AddRange(await db.DialogueLines
            .AsNoTracking()
            .Where(line => line.SpeakerNpcId == entityId)
            .Select(line => new EntityReferenceHit(
                line.DialogueId, ModuleKeys.Dialogs, line.Dialogue!.Name, "Sprecher"))
            .Distinct()
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Karten für die modulübergreifenden Dienste. Ihre Markierungen verweisen über eigene
/// Spalten auf beliebige andere Entitäten.
/// </summary>
public sealed class MapEntitySource : ModuleEntitySource<GameMap>
{
    public override string ModuleKey => ModuleKeys.Maps;

    protected override DbSet<GameMap> Set(GameDevManagerDbContext db) => db.Maps;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<GameMap> query) =>
        query.Select(map => new SearchHit(
            map.Id,
            ModuleKeys.Maps,
            SearchHitKind.Entity,
            map.Name,
            map.Markers.Count + " Markierung(en)",
            db.Assets.Where(a => a.OwnerEntityId == map.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        db.MapMarkers
            .AsNoTracking()
            .Where(marker => marker.TargetEntityId == entityId)
            .Select(marker => new EntityReferenceHit(
                marker.MapId,
                ModuleKeys.Maps,
                marker.Map!.Name,
                marker.Radius > 0 ? "Bereich auf der Karte" : "Markierung auf der Karte"))
            .ToListAsync(ct);
}

/// <summary>
/// Loot-Tables für die modulübergreifenden Dienste. Ihre Einträge verweisen über eigene
/// Spalten auf Items.
/// </summary>
public sealed class LootTableEntitySource : ModuleEntitySource<LootTable>
{
    public override string ModuleKey => ModuleKeys.Loot;

    protected override DbSet<LootTable> Set(GameDevManagerDbContext db) => db.LootTables;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<LootTable> query) =>
        query.Select(table => new SearchHit(
            table.Id,
            ModuleKeys.Loot,
            SearchHitKind.Entity,
            table.Name,
            table.Entries.Count + " Eintrag/Einträge",
            null));

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        db.LootEntries
            .AsNoTracking()
            .Where(entry => entry.ItemId == entityId)
            .Select(entry => new EntityReferenceHit(
                entry.LootTableId, ModuleKeys.Loot, entry.LootTable!.Name, "Loot-Eintrag"))
            .ToListAsync(ct);
}

/// <summary>
/// Fraktionen für die modulübergreifenden Dienste. Ihre Mitgliederliste verweist über
/// eigene Spalten auf NPCs.
/// </summary>
public sealed class FactionEntitySource : ModuleEntitySource<Faction>
{
    public override string ModuleKey => ModuleKeys.Factions;

    protected override DbSet<Faction> Set(GameDevManagerDbContext db) => db.Factions;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Faction> query) =>
        query.Select(faction => new SearchHit(
            faction.Id,
            ModuleKeys.Factions,
            SearchHitKind.Entity,
            faction.Name,
            faction.Members.Count + " Mitglied(er)",
            db.Assets.Where(a => a.OwnerEntityId == faction.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        db.FactionMembers
            .AsNoTracking()
            .Where(member => member.NpcId == entityId)
            .Select(member => new EntityReferenceHit(
                member.FactionId,
                ModuleKeys.Factions,
                member.Faction!.Name,
                member.Role ?? "Mitglied"))
            .ToListAsync(ct);
}

/// <summary>
/// Diplomatische Beziehungen für die modulübergreifenden Dienste. Beide Seiten der
/// Beziehung verweisen über eigene Spalten auf Fraktionen.
/// </summary>
public sealed class DiplomaticRelationEntitySource : ModuleEntitySource<DiplomaticRelation>
{
    public override string ModuleKey => ModuleKeys.Diplomacy;

    protected override DbSet<DiplomaticRelation> Set(GameDevManagerDbContext db) => db.DiplomaticRelations;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<DiplomaticRelation> query) =>
        query.Select(relation => new SearchHit(
            relation.Id,
            ModuleKeys.Diplomacy,
            SearchHitKind.Entity,
            relation.Name,
            relation.Stance == DiplomaticStance.Alliance ? "Allianz"
                : relation.Stance == DiplomaticStance.Friendship ? "Freundschaft"
                : relation.Stance == DiplomaticStance.Hostility ? "Feindschaft"
                : relation.Stance == DiplomaticStance.War ? "Krieg"
                : "Neutral",
            null));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.DiplomaticRelations
            .AsNoTracking()
            .Where(relation => relation.FactionAId == entityId || relation.FactionBId == entityId)
            .Select(relation => new EntityReferenceHit(
                relation.Id, ModuleKeys.Diplomacy, relation.Name, "Diplomatische Beziehung"))
            .ToListAsync(ct);

        return hits;
    }
}

/// <summary>
/// Story-Abschnitte für die modulübergreifenden Dienste. Ihre Beteiligten verweisen über
/// eigene Spalten auf beliebige andere Entitäten.
/// </summary>
public sealed class StoryEntrySource : ModuleEntitySource<StoryEntry>
{
    public override string ModuleKey => ModuleKeys.Story;

    protected override DbSet<StoryEntry> Set(GameDevManagerDbContext db) => db.StoryEntries;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<StoryEntry> query) =>
        query.Select(entry => new SearchHit(
            entry.Id,
            ModuleKeys.Story,
            SearchHitKind.Entity,
            entry.Name,
            "Abschnitt " + (entry.SortOrder + 1),
            db.Assets.Where(a => a.OwnerEntityId == entry.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        db.StoryParticipants
            .AsNoTracking()
            .Where(participant => participant.TargetEntityId == entityId)
            .Select(participant => new EntityReferenceHit(
                participant.StoryEntryId,
                ModuleKeys.Story,
                participant.StoryEntry!.Name,
                "Beteiligt an der Story"))
            .ToListAsync(ct);
}

/// <summary>
/// Quests für die modulübergreifenden Dienste. Questgeber, Story-Anbindung und Dialog
/// verweisen über eigene Spalten auf fremde Entitäten.
/// </summary>
public sealed class QuestEntitySource : ModuleEntitySource<Quest>
{
    public override string ModuleKey => ModuleKeys.Quests;

    protected override DbSet<Quest> Set(GameDevManagerDbContext db) => db.Quests;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Quest> query) =>
        query.Select(quest => new SearchHit(
            quest.Id,
            ModuleKeys.Quests,
            SearchHitKind.Entity,
            quest.Name,
            quest.Kind == QuestKind.MainMission ? "Hauptmission"
                : quest.Kind == QuestKind.Event ? "Event"
                : "Nebenmission",
            db.Assets.Where(a => a.OwnerEntityId == quest.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.Quests
            .AsNoTracking()
            .Where(quest => quest.GiverNpcId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, "Questgeber"))
            .ToListAsync(ct);

        hits.AddRange(await db.Quests
            .AsNoTracking()
            .Where(quest => quest.StoryEntryId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, "Story-Anbindung"))
            .ToListAsync(ct));

        hits.AddRange(await db.Quests
            .AsNoTracking()
            .Where(quest => quest.DialogueId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, "Dialog zur Quest"))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Zufalls-Events für die modulübergreifenden Dienste. Spawns und Belohnung verweisen über
/// eigene Spalten auf NPCs und Loot-Tables.
/// </summary>
public sealed class GameEventEntitySource : ModuleEntitySource<GameEvent>
{
    public override string ModuleKey => ModuleKeys.Events;

    protected override DbSet<GameEvent> Set(GameDevManagerDbContext db) => db.GameEvents;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<GameEvent> query) =>
        query.Select(gameEvent => new SearchHit(
            gameEvent.Id,
            ModuleKeys.Events,
            SearchHitKind.Entity,
            gameEvent.Name,
            gameEvent.Chance + " %",
            db.Assets.Where(a => a.OwnerEntityId == gameEvent.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.EventSpawns
            .AsNoTracking()
            .Where(spawn => spawn.NpcId == entityId)
            .Select(spawn => new EntityReferenceHit(
                spawn.GameEventId, ModuleKeys.Events, spawn.GameEvent!.Name, "Spawnt beim Event"))
            .ToListAsync(ct);

        hits.AddRange(await db.GameEvents
            .AsNoTracking()
            .Where(gameEvent => gameEvent.RewardLootTableId == entityId)
            .Select(gameEvent => new EntityReferenceHit(
                gameEvent.Id, ModuleKeys.Events, gameEvent.Name, "Event-Belohnung"))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Skills für die modulübergreifenden Dienste. Kosten-Item und Voraussetzung verweisen über
/// eigene Spalten auf Items bzw. andere Skills.
/// </summary>
public sealed class SkillEntitySource : ModuleEntitySource<Skill>
{
    public override string ModuleKey => ModuleKeys.Player;

    protected override DbSet<Skill> Set(GameDevManagerDbContext db) => db.Skills;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Skill> query) =>
        query.Select(skill => new SearchHit(
            skill.Id,
            ModuleKeys.Player,
            SearchHitKind.Entity,
            skill.Name,
            db.SkillTrees.Where(t => t.Id == skill.SkillTreeId).Select(t => t.Name).FirstOrDefault() ?? "Skill",
            db.Assets.Where(a => a.OwnerEntityId == skill.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var hits = await db.Skills
            .AsNoTracking()
            .Where(skill => skill.CostItemId == entityId)
            .Select(skill => new EntityReferenceHit(skill.Id, ModuleKeys.Player, skill.Name, "Skill-Kosten"))
            .ToListAsync(ct);

        hits.AddRange(await db.Skills
            .AsNoTracking()
            .Where(skill => skill.ParentSkillId == entityId)
            .Select(skill => new EntityReferenceHit(skill.Id, ModuleKeys.Player, skill.Name, "Setzt diesen Skill voraus"))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Klassen für die modulübergreifenden Dienste. NPCs verweisen über ihre Klassen-Spalte
/// hierher; das meldet die NPC-Quelle nicht, deshalb steht der Rückwärtsblick hier.
/// </summary>
public sealed class CharacterClassEntitySource : ModuleEntitySource<CharacterClass>
{
    public override string ModuleKey => ModuleKeys.Classes;

    protected override DbSet<CharacterClass> Set(GameDevManagerDbContext db) => db.CharacterClasses;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<CharacterClass> query) =>
        query.Select(characterClass => new SearchHit(
            characterClass.Id,
            ModuleKeys.Classes,
            SearchHitKind.Entity,
            characterClass.Name,
            characterClass.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == characterClass.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        // Die Spielerfiguren-Seite hat keine Detailroute je Figur; ihre Klassenzuordnung
        // zeigt die Klassen-Maske selbst, deshalb hier nur die NPCs.
        db.Npcs
            .AsNoTracking()
            .Where(npc => npc.CharacterClassId == entityId)
            .Select(npc => new EntityReferenceHit(npc.Id, ModuleKeys.Npcs, npc.Name, "Klasse"))
            .ToListAsync(ct);
}

/// <summary>
/// Währungen für die modulübergreifenden Dienste.
/// </summary>
public sealed class CurrencyEntitySource : ModuleEntitySource<Currency>
{
    public override string ModuleKey => ModuleKeys.Currencies;

    protected override DbSet<Currency> Set(GameDevManagerDbContext db) => db.Currencies;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Currency> query) =>
        query.Select(currency => new SearchHit(
            currency.Id,
            ModuleKeys.Currencies,
            SearchHitKind.Entity,
            currency.Name,
            currency.Symbol,
            db.Assets.Where(a => a.OwnerEntityId == currency.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
}
