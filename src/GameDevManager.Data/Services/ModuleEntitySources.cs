using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Items für die modulübergreifenden Dienste.
/// </summary>
public sealed class ItemEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Item>(messages)
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
public sealed class RecipeEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Recipe>(messages)
{
    public override string ModuleKey => ModuleKeys.Crafting;

    protected override DbSet<Recipe> Set(GameDevManagerDbContext db) => db.Recipes;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Recipe> query)
    {
        var yields = Messages["Search_RecipeYields"].Value;

        return query.Select(recipe => new SearchHit(
            recipe.Id,
            ModuleKeys.Crafting,
            SearchHitKind.Entity,
            recipe.Name,
            db.Items.Where(i => i.Id == recipe.OutputItemId).Select(i => yields + i.Name).FirstOrDefault(),
            db.Assets.Where(a => a.OwnerEntityId == recipe.OutputItemId && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var recipeOutput = Messages["Reference_RecipeOutput"].Value;
        var recipeIngredient = Messages["Reference_RecipeIngredient"].Value;

        var hits = await db.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.OutputItemId == entityId)
            .Select(recipe => new EntityReferenceHit(recipe.Id, ModuleKeys.Crafting, recipe.Name, recipeOutput))
            .ToListAsync(ct);

        hits.AddRange(await db.RecipeIngredients
            .AsNoTracking()
            .Where(ingredient => ingredient.ItemId == entityId)
            .Select(ingredient => new EntityReferenceHit(
                ingredient.RecipeId, ModuleKeys.Crafting, ingredient.Recipe!.Name, recipeIngredient))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// NPCs und Mobs für die modulübergreifenden Dienste. Ihre Warenangebote verweisen über
/// eigene Spalten auf Items und Währungen.
/// </summary>
public sealed class NpcEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Npc>(messages)
{
    public override string ModuleKey => ModuleKeys.Npcs;

    protected override DbSet<Npc> Set(GameDevManagerDbContext db) => db.Npcs;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Npc> query)
    {
        var mob = Messages["Search_Mob"].Value;
        var trader = Messages["Search_Trader"].Value;

        return query.Select(npc => new SearchHit(
            npc.Id,
            ModuleKeys.Npcs,
            SearchHitKind.Entity,
            npc.Name,
            npc.Kind == NpcKind.Mob ? mob : npc.IsTrader ? trader : npc.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == npc.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var tradeGoods = Messages["Reference_TradeGoods"].Value;
        var offerCurrency = Messages["Reference_OfferCurrency"].Value;
        var lootTable = Messages["Reference_LootTable"].Value;

        var hits = await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.ItemId == entityId)
            .Select(offer => new EntityReferenceHit(
                offer.NpcId, ModuleKeys.Npcs, offer.Npc!.Name, tradeGoods))
            .ToListAsync(ct);

        hits.AddRange(await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.CurrencyId == entityId)
            .Select(offer => new EntityReferenceHit(
                offer.NpcId, ModuleKeys.Npcs, offer.Npc!.Name, offerCurrency))
            .Distinct()
            .ToListAsync(ct));

        hits.AddRange(await db.Npcs
            .AsNoTracking()
            .Where(npc => npc.LootTableId == entityId)
            .Select(npc => new EntityReferenceHit(npc.Id, ModuleKeys.Npcs, npc.Name, lootTable))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Dialoge für die modulübergreifenden Dienste. Beteiligte und Sprecher verweisen über
/// eigene Spalten auf NPCs.
/// </summary>
public sealed class DialogueEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Dialogue>(messages)
{
    public override string ModuleKey => ModuleKeys.Dialogs;

    protected override DbSet<Dialogue> Set(GameDevManagerDbContext db) => db.Dialogues;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Dialogue> query)
    {
        var barks = Messages["Search_Barks"].Value;
        var lineSuffix = Messages["Search_LineCount"].Value;

        return query.Select(dialogue => new SearchHit(
            dialogue.Id,
            ModuleKeys.Dialogs,
            SearchHitKind.Entity,
            dialogue.Name,
            dialogue.Kind == DialogueKind.Bark ? barks : dialogue.Lines.Count + lineSuffix,
            null));
    }

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
public sealed class MapEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<GameMap>(messages)
{
    public override string ModuleKey => ModuleKeys.Maps;

    protected override DbSet<GameMap> Set(GameDevManagerDbContext db) => db.Maps;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<GameMap> query)
    {
        var markerSuffix = Messages["Search_MarkerCount"].Value;

        return query.Select(map => new SearchHit(
            map.Id,
            ModuleKeys.Maps,
            SearchHitKind.Entity,
            map.Name,
            map.Markers.Count + markerSuffix,
            db.Assets.Where(a => a.OwnerEntityId == map.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var mapArea = Messages["Reference_MapArea"].Value;
        var mapMarker = Messages["Reference_MapMarker"].Value;

        return db.MapMarkers
            .AsNoTracking()
            .Where(marker => marker.TargetEntityId == entityId)
            .Select(marker => new EntityReferenceHit(
                marker.MapId,
                ModuleKeys.Maps,
                marker.Map!.Name,
                marker.Radius > 0 ? mapArea : mapMarker))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Loot-Tables für die modulübergreifenden Dienste. Ihre Einträge verweisen über eigene
/// Spalten auf Items.
/// </summary>
public sealed class LootTableEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<LootTable>(messages)
{
    public override string ModuleKey => ModuleKeys.Loot;

    protected override DbSet<LootTable> Set(GameDevManagerDbContext db) => db.LootTables;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<LootTable> query)
    {
        var entrySuffix = Messages["Search_EntryCount"].Value;

        return query.Select(table => new SearchHit(
            table.Id,
            ModuleKeys.Loot,
            SearchHitKind.Entity,
            table.Name,
            table.Entries.Count + entrySuffix,
            null));
    }

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
public sealed class FactionEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Faction>(messages)
{
    public override string ModuleKey => ModuleKeys.Factions;

    protected override DbSet<Faction> Set(GameDevManagerDbContext db) => db.Factions;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Faction> query)
    {
        var memberSuffix = Messages["Search_MemberCount"].Value;

        return query.Select(faction => new SearchHit(
            faction.Id,
            ModuleKeys.Factions,
            SearchHitKind.Entity,
            faction.Name,
            faction.Members.Count + memberSuffix,
            db.Assets.Where(a => a.OwnerEntityId == faction.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var memberRole = Messages["Reference_FactionMember"].Value;

        return db.FactionMembers
            .AsNoTracking()
            .Where(member => member.NpcId == entityId)
            .Select(member => new EntityReferenceHit(
                member.FactionId,
                ModuleKeys.Factions,
                member.Faction!.Name,
                member.Role ?? memberRole))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Diplomatische Beziehungen für die modulübergreifenden Dienste. Beide Seiten der
/// Beziehung verweisen über eigene Spalten auf Fraktionen.
/// </summary>
public sealed class DiplomaticRelationEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<DiplomaticRelation>(messages)
{
    public override string ModuleKey => ModuleKeys.Diplomacy;

    protected override DbSet<DiplomaticRelation> Set(GameDevManagerDbContext db) => db.DiplomaticRelations;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<DiplomaticRelation> query)
    {
        var alliance = Messages["Stance_Alliance"].Value;
        var friendship = Messages["Stance_Friendship"].Value;
        var hostility = Messages["Stance_Hostility"].Value;
        var war = Messages["Stance_War"].Value;
        var neutral = Messages["Stance_Neutral"].Value;

        return query.Select(relation => new SearchHit(
            relation.Id,
            ModuleKeys.Diplomacy,
            SearchHitKind.Entity,
            relation.Name,
            relation.Stance == DiplomaticStance.Alliance ? alliance
                : relation.Stance == DiplomaticStance.Friendship ? friendship
                : relation.Stance == DiplomaticStance.Hostility ? hostility
                : relation.Stance == DiplomaticStance.War ? war
                : neutral,
            null));
    }

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
public sealed class StoryEntrySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<StoryEntry>(messages)
{
    public override string ModuleKey => ModuleKeys.Story;

    protected override DbSet<StoryEntry> Set(GameDevManagerDbContext db) => db.StoryEntries;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<StoryEntry> query)
    {
        var chapter = Messages["Search_StoryChapter"].Value;

        return query.Select(entry => new SearchHit(
            entry.Id,
            ModuleKeys.Story,
            SearchHitKind.Entity,
            entry.Name,
            chapter + (entry.SortOrder + 1),
            db.Assets.Where(a => a.OwnerEntityId == entry.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var storyParticipant = Messages["Reference_StoryParticipant"].Value;

        return db.StoryParticipants
            .AsNoTracking()
            .Where(participant => participant.TargetEntityId == entityId)
            .Select(participant => new EntityReferenceHit(
                participant.StoryEntryId,
                ModuleKeys.Story,
                participant.StoryEntry!.Name,
                storyParticipant))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Quests für die modulübergreifenden Dienste. Questgeber, Story-Anbindung und Dialog
/// verweisen über eigene Spalten auf fremde Entitäten.
/// </summary>
public sealed class QuestEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Quest>(messages)
{
    public override string ModuleKey => ModuleKeys.Quests;

    protected override DbSet<Quest> Set(GameDevManagerDbContext db) => db.Quests;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Quest> query)
    {
        var mainMission = Messages["QuestKind_MainMission"].Value;
        var eventKind = Messages["QuestKind_Event"].Value;
        var sideMission = Messages["QuestKind_SideMission"].Value;

        return query.Select(quest => new SearchHit(
            quest.Id,
            ModuleKeys.Quests,
            SearchHitKind.Entity,
            quest.Name,
            quest.Kind == QuestKind.MainMission ? mainMission
                : quest.Kind == QuestKind.Event ? eventKind
                : sideMission,
            db.Assets.Where(a => a.OwnerEntityId == quest.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var questStory = Messages["Reference_QuestStory"].Value;
        var questDialogue = Messages["Reference_QuestDialogue"].Value;

        var hits = await db.Quests
            .AsNoTracking()
            .Where(quest => quest.GiverNpcId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, "Questgeber"))
            .ToListAsync(ct);

        hits.AddRange(await db.Quests
            .AsNoTracking()
            .Where(quest => quest.StoryEntryId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, questStory))
            .ToListAsync(ct));

        hits.AddRange(await db.Quests
            .AsNoTracking()
            .Where(quest => quest.DialogueId == entityId)
            .Select(quest => new EntityReferenceHit(quest.Id, ModuleKeys.Quests, quest.Name, questDialogue))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Zufalls-Events für die modulübergreifenden Dienste. Spawns und Belohnung verweisen über
/// eigene Spalten auf NPCs und Loot-Tables.
/// </summary>
public sealed class GameEventEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<GameEvent>(messages)
{
    public override string ModuleKey => ModuleKeys.Events;

    protected override DbSet<GameEvent> Set(GameDevManagerDbContext db) => db.GameEvents;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<GameEvent> query)
    {
        var percentSuffix = Messages["Search_PercentSuffix"].Value;

        return query.Select(gameEvent => new SearchHit(
            gameEvent.Id,
            ModuleKeys.Events,
            SearchHitKind.Entity,
            gameEvent.Name,
            gameEvent.Chance + percentSuffix,
            db.Assets.Where(a => a.OwnerEntityId == gameEvent.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var eventSpawn = Messages["Reference_EventSpawn"].Value;

        var hits = await db.EventSpawns
            .AsNoTracking()
            .Where(spawn => spawn.NpcId == entityId)
            .Select(spawn => new EntityReferenceHit(
                spawn.GameEventId, ModuleKeys.Events, spawn.GameEvent!.Name, eventSpawn))
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
public sealed class SkillEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Skill>(messages)
{
    public override string ModuleKey => ModuleKeys.Player;

    protected override DbSet<Skill> Set(GameDevManagerDbContext db) => db.Skills;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Skill> query)
    {
        var skillFallback = Messages["Search_SkillFallback"].Value;

        return query.Select(skill => new SearchHit(
            skill.Id,
            ModuleKeys.Player,
            SearchHitKind.Entity,
            skill.Name,
            db.SkillTrees.Where(t => t.Id == skill.SkillTreeId).Select(t => t.Name).FirstOrDefault() ?? skillFallback,
            db.Assets.Where(a => a.OwnerEntityId == skill.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var skillCost = Messages["Reference_SkillCost"].Value;
        var skillParent = Messages["Reference_SkillParent"].Value;

        var hits = await db.Skills
            .AsNoTracking()
            .Where(skill => skill.CostItemId == entityId)
            .Select(skill => new EntityReferenceHit(skill.Id, ModuleKeys.Player, skill.Name, skillCost))
            .ToListAsync(ct);

        hits.AddRange(await db.Skills
            .AsNoTracking()
            .Where(skill => skill.ParentSkillId == entityId)
            .Select(skill => new EntityReferenceHit(skill.Id, ModuleKeys.Player, skill.Name, skillParent))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Klassen für die modulübergreifenden Dienste. NPCs verweisen über ihre Klassen-Spalte
/// hierher; das meldet die NPC-Quelle nicht, deshalb steht der Rückwärtsblick hier.
/// </summary>
public sealed class CharacterClassEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<CharacterClass>(messages)
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
            .Select(npc => new EntityReferenceHit(npc.Id, ModuleKeys.Npcs, npc.Name, Messages["Reference_Class"].Value))
            .ToListAsync(ct);
}

/// <summary>
/// Effekte für die modulübergreifenden Dienste. Ihre Zuweisungen verweisen über eigene
/// Spalten auf Items.
/// </summary>
public sealed class GameEffectEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<GameEffect>(messages)
{
    public override string ModuleKey => ModuleKeys.Effects;

    protected override DbSet<GameEffect> Set(GameDevManagerDbContext db) => db.GameEffects;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<GameEffect> query)
    {
        var itemSuffix = Messages["Search_ItemCount"].Value;

        return query.Select(effect => new SearchHit(
            effect.Id,
            ModuleKeys.Effects,
            SearchHitKind.Entity,
            effect.Name,
            effect.Assignments.Count + itemSuffix,
            db.Assets.Where(a => a.OwnerEntityId == effect.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var itemEffect = Messages["Reference_ItemEffect"].Value;

        return db.EffectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ItemId == entityId)
            .Select(assignment => new EntityReferenceHit(
                assignment.GameEffectId,
                ModuleKeys.Effects,
                assignment.GameEffect!.Name,
                itemEffect))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Achievements für die modulübergreifenden Dienste.
/// </summary>
public sealed class AchievementEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Achievement>(messages)
{
    public override string ModuleKey => ModuleKeys.Achievements;

    protected override DbSet<Achievement> Set(GameDevManagerDbContext db) => db.Achievements;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Achievement> query)
    {
        var secret = Messages["Search_Secret"].Value;

        return query.Select(achievement => new SearchHit(
            achievement.Id,
            ModuleKeys.Achievements,
            SearchHitKind.Entity,
            achievement.Name,
            achievement.IsSecret ? secret : achievement.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == achievement.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }
}

/// <summary>
/// Sammelobjekte für die modulübergreifenden Dienste.
/// </summary>
public sealed class CollectibleEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Collectible>(messages)
{
    public override string ModuleKey => ModuleKeys.Collectibles;

    protected override DbSet<Collectible> Set(GameDevManagerDbContext db) => db.Collectibles;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Collectible> query) =>
        query.Select(collectible => new SearchHit(
            collectible.Id,
            ModuleKeys.Collectibles,
            SearchHitKind.Entity,
            collectible.Name,
            collectible.ContentType!.Name,
            db.Assets.Where(a => a.OwnerEntityId == collectible.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
}

/// <summary>
/// Sounds für die modulübergreifenden Dienste.
/// </summary>
public sealed class SoundEffectEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<SoundEffect>(messages)
{
    public override string ModuleKey => ModuleKeys.Audio;

    protected override DbSet<SoundEffect> Set(GameDevManagerDbContext db) => db.SoundEffects;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<SoundEffect> query)
    {
        var fileSuffix = Messages["Search_FileCount"].Value;

        return query.Select(sound => new SearchHit(
            sound.Id,
            ModuleKeys.Audio,
            SearchHitKind.Entity,
            sound.Name,
            db.Assets.Count(a => a.OwnerEntityId == sound.Id && a.MimeType.StartsWith("audio/")) + fileSuffix,
            db.Assets.Where(a => a.OwnerEntityId == sound.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }
}

/// <summary>
/// Cutscenes für die modulübergreifenden Dienste. Story-Anbindung und Dialog verweisen über
/// eigene Spalten auf fremde Entitäten.
/// </summary>
public sealed class CutsceneEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Cutscene>(messages)
{
    public override string ModuleKey => ModuleKeys.Cutscenes;

    protected override DbSet<Cutscene> Set(GameDevManagerDbContext db) => db.Cutscenes;

    protected override IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<Cutscene> query)
    {
        var shotSuffix = Messages["Search_ShotCount"].Value;

        return query.Select(cutscene => new SearchHit(
            cutscene.Id,
            ModuleKeys.Cutscenes,
            SearchHitKind.Entity,
            cutscene.Name,
            cutscene.Shots.Count + shotSuffix,
            db.Assets.Where(a => a.OwnerEntityId == cutscene.Id && a.IsPrimary)
                .Select(a => (Guid?)a.Id).FirstOrDefault()));
    }

    public override async Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var cutsceneStory = Messages["Reference_CutsceneStory"].Value;
        var cutsceneDialogue = Messages["Reference_CutsceneDialogue"].Value;

        var hits = await db.Cutscenes
            .AsNoTracking()
            .Where(cutscene => cutscene.StoryEntryId == entityId)
            .Select(cutscene => new EntityReferenceHit(
                cutscene.Id, ModuleKeys.Cutscenes, cutscene.Name, cutsceneStory))
            .ToListAsync(ct);

        hits.AddRange(await db.Cutscenes
            .AsNoTracking()
            .Where(cutscene => cutscene.DialogueId == entityId)
            .Select(cutscene => new EntityReferenceHit(
                cutscene.Id, ModuleKeys.Cutscenes, cutscene.Name, cutsceneDialogue))
            .ToListAsync(ct));

        return hits;
    }
}

/// <summary>
/// Währungen für die modulübergreifenden Dienste.
/// </summary>
public sealed class CurrencyEntitySource(IStringLocalizer<DataMessages> messages)
    : ModuleEntitySource<Currency>(messages)
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
