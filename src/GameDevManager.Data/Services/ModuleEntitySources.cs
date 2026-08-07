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
