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

        return hits;
    }
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
