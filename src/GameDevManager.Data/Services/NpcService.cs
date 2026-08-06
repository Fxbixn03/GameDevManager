using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben von NPCs und Mobs samt Warenangebot und benutzerdefinierten Feldwerten.
/// </summary>
public class NpcService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets)
{
    public async Task<List<NpcListRow>> GetNpcsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Npcs
            .AsNoTracking()
            .Where(n => n.GameProjectId == projectId)
            .OrderBy(n => n.Name)
            .Select(n => new NpcListRow(
                n.Id,
                n.Name,
                n.Description,
                n.Kind,
                n.IsTrader,
                n.IsQuestGiver,
                n.LootTableId != null,
                n.ContentTypeId,
                n.ContentType!.Name,
                n.Offers.Count,
                n.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == n.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Händler, die ein Item führen — für die Item-Maske und die Frage, ob ein Item
    /// überhaupt eine Bezugsquelle hat (im Konzept ein Health Check: „toter Content“).
    /// </summary>
    public async Task<List<TraderForItem>> GetTradersForItemAsync(
        Guid projectId, Guid itemId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.TraderOffers
            .AsNoTracking()
            .Where(offer => offer.ItemId == itemId && offer.Npc!.GameProjectId == projectId)
            .OrderBy(offer => offer.Npc!.Name)
            .Select(offer => new TraderForItem(
                offer.NpcId,
                offer.Npc!.Name,
                offer.SellPrice,
                offer.BuyPrice,
                db.Currencies.Where(c => c.Id == offer.CurrencyId).Select(c => c.Symbol ?? c.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Npc>?> LoadForEditAsync(
        Guid projectId, Guid? npcId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Npcs, ct);

        if (npcId is null)
        {
            return new ContentEditContext<Npc>
            {
                Entity = new Npc { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var npc = await db.Npcs
            .AsNoTracking()
            .Include(n => n.Offers)
            .FirstOrDefaultAsync(n => n.Id == npcId && n.GameProjectId == projectId, ct);

        if (npc is null)
        {
            return null;
        }

        npc.Offers = [.. npc.Offers.OrderBy(offer => offer.SortOrder)];

        return new ContentEditContext<Npc>
        {
            Entity = npc,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, npc.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, npc.Id, ct)
        };
    }

    public async Task SaveNpcAsync(ContentEditContext<Npc> context, CancellationToken ct = default)
    {
        var npc = context.Entity;

        if (string.IsNullOrWhiteSpace(npc.Name))
        {
            throw new ContentValidationException("Der NPC braucht einen Namen.");
        }

        Validate(npc);
        ContentFields.ValidateRequired(context);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Npcs
            .Include(n => n.Offers)
            .FirstOrDefaultAsync(n => n.Id == npc.Id, ct);

        if (stored is null)
        {
            stored = new Npc
            {
                Id = npc.Id,
                GameProjectId = npc.GameProjectId,
                Name = npc.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Npcs.Add(stored);
        }

        stored.ContentTypeId = npc.ContentTypeId;
        stored.Name = npc.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(npc.Description) ? null : npc.Description.Trim();
        stored.Kind = npc.Kind;
        stored.IsTrader = npc.IsTrader;
        stored.IsQuestGiver = npc.IsQuestGiver;
        stored.LootTableId = npc.LootTableId;
        stored.UpdatedAtUtc = now;

        SyncOffers(db, stored, npc);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        npc.CreatedAtUtc = stored.CreatedAtUtc;
        npc.UpdatedAtUtc = stored.UpdatedAtUtc;
        npc.Name = stored.Name;
        npc.Description = stored.Description;
    }

    private static void Validate(Npc npc)
    {
        // Ein Angebot ohne Item ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (npc.IsTrader && npc.Offers.Any(offer => offer.ItemId == Guid.Empty))
        {
            throw new ContentValidationException("Jedes Angebot braucht ein Item.");
        }

        var duplicate = npc.Offers
            .GroupBy(offer => offer.ItemId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(
                "Dasselbe Item steht mehrfach im Angebot. Bitte die Posten zusammenfassen.");
        }

        foreach (var offer in npc.Offers)
        {
            if (offer.SellPrice < 0 || offer.BuyPrice < 0)
            {
                throw new ContentValidationException("Preise dürfen nicht negativ sein.");
            }

            if (offer.Stock < 0)
            {
                throw new ContentValidationException("Der Lagerbestand darf nicht negativ sein.");
            }

            if (offer.RestockSeconds < 0)
            {
                throw new ContentValidationException("Die Auffüllzeit darf nicht negativ sein.");
            }

            if (offer.CurrencyId is null && (offer.SellPrice is not null || offer.BuyPrice is not null))
            {
                throw new ContentValidationException(
                    "Zu einem Preis gehört eine Währung — sonst ist die Zahl nicht zu deuten.");
            }
        }
    }

    private static void SyncOffers(GameDevManagerDbContext db, Npc stored, Npc incoming)
    {
        var wanted = incoming.Offers;
        var wantedIds = wanted.Select(offer => offer.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Offers.Where(o => !wantedIds.Contains(o.Id)).ToList())
        {
            stored.Offers.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var offer = wanted[index];
            var target = stored.Offers.FirstOrDefault(o => o.Id == offer.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet: der Posten bringt seine GUID schon mit, und EF
                // hielte ihn beim Anhängen an einen bestehenden NPC sonst für einen vorhandenen
                // Datensatz — es entstünde ein UPDATE auf eine Zeile, die es noch nicht gibt.
                db.TraderOffers.Add(new TraderOffer
                {
                    Id = offer.Id,
                    NpcId = stored.Id,
                    ItemId = offer.ItemId,
                    CurrencyId = offer.CurrencyId,
                    SellPrice = offer.SellPrice,
                    BuyPrice = offer.BuyPrice,
                    Stock = offer.Stock,
                    RestockSeconds = offer.RestockSeconds,
                    SortOrder = index
                });
            }
            else
            {
                target.ItemId = offer.ItemId;
                target.CurrencyId = offer.CurrencyId;
                target.SellPrice = offer.SellPrice;
                target.BuyPrice = offer.BuyPrice;
                target.Stock = offer.Stock;
                target.RestockSeconds = offer.RestockSeconds;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht einen NPC mit Angebot, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteNpcAsync(Guid npcId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(npcId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ContentFields.DeleteForEntityAsync(db, npcId, ct);

        // Die Angebote fallen über den Fremdschlüssel mit.
        await db.Npcs
            .Where(n => n.Id == npcId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
