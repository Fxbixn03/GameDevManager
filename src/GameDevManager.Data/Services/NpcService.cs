using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben von NPCs und Mobs samt Warenangebot und benutzerdefinierten Feldwerten.
/// </summary>
public class NpcService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
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

    /// <summary>
    /// Die Spawn-Regeln, die auf eine Karte zeigen — für die Aufklappliste im Karten-Editor.
    /// Ob eine Regel bedingt ist, steht als Schalter daneben: Der Bedingungssatz hängt im Slot
    /// <see cref="ConditionSlots.Spawn"/> an der GUID der Regel.
    /// </summary>
    public async Task<List<MapSpawnRuleRow>> GetSpawnRulesForMapAsync(
        Guid mapId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var rules = await db.SpawnRules
            .AsNoTracking()
            .Where(rule => rule.TargetMapId == mapId)
            .OrderBy(rule => rule.Npc!.Name)
            .ThenBy(rule => rule.SortOrder)
            .Select(rule => new
            {
                rule.Id,
                rule.NpcId,
                NpcName = rule.Npc!.Name,
                rule.TargetMarkerId,
                rule.MinCount,
                rule.MaxCount,
                rule.RespawnSeconds
            })
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            return [];
        }

        var ruleIds = rules.Select(rule => rule.Id).ToList();
        var conditioned = await db.ConditionSets
            .AsNoTracking()
            .Where(set => ruleIds.Contains(set.OwnerId) && set.Slot == ConditionSlots.Spawn)
            .Select(set => set.OwnerId)
            .ToListAsync(ct);

        return
        [
            .. rules.Select(rule => new MapSpawnRuleRow(
                rule.Id,
                rule.NpcId,
                rule.NpcName,
                rule.TargetMarkerId,
                rule.MinCount,
                rule.MaxCount,
                rule.RespawnSeconds,
                conditioned.Contains(rule.Id)))
        ];
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
            .Include(n => n.Relations)
            .Include(n => n.SpawnRules)
            .FirstOrDefaultAsync(n => n.Id == npcId && n.GameProjectId == projectId, ct);

        if (npc is null)
        {
            return null;
        }

        npc.Offers = [.. npc.Offers.OrderBy(offer => offer.SortOrder)];
        npc.Relations = [.. npc.Relations.OrderBy(relation => relation.SortOrder)];

        return new ContentEditContext<Npc>
        {
            Entity = npc,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, npc.Id, ct),
            Values = await ContentFields.LoadValuesAsync<Npc>(db, npc.Id, ct)
        };
    }

    public async Task SaveNpcAsync(ContentEditContext<Npc> context, CancellationToken ct = default)
    {
        var npc = context.Entity;

        if (string.IsNullOrWhiteSpace(npc.Name))
        {
            throw new ContentValidationException(messages["NpcNameRequired"]);
        }

        Validate(npc);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Npcs
            .Include(n => n.Offers)
            .Include(n => n.Relations)
            .Include(n => n.SpawnRules)
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
        stored.IsUnique = npc.IsUnique;
        stored.IsTrader = npc.IsTrader;
        stored.IsQuestGiver = npc.IsQuestGiver;
        stored.LootTableId = npc.LootTableId;
        stored.CharacterClassId = npc.CharacterClassId;
        stored.Preferences = KeywordList.Normalize(npc.Preferences);
        stored.Personality = KeywordList.Normalize(npc.Personality);
        // Über den Umweg Parse/Format wird die Spalte kanonisch — derselbe Stand ergibt
        // denselben Export.
        stored.Traits = NpcTraits.Format(NpcTraits.Parse(npc.Traits));
        stored.UpdatedAtUtc = now;

        var removedOfferIds = new List<Guid>();
        SyncOffers(db, stored, npc, removedOfferIds);
        SyncRelations(db, stored, npc);
        SyncSpawnRules(db, stored, npc, removedOfferIds);

        // Bedingungen entfernter Posten und Spawn-Regeln hängen an deren GUID und fallen
        // nicht von selbst mit.
        await EntityCleanup.DeleteForSubObjectsAsync(db, removedOfferIds, ct);

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        npc.CreatedAtUtc = stored.CreatedAtUtc;
        npc.UpdatedAtUtc = stored.UpdatedAtUtc;
        npc.Name = stored.Name;
        npc.Description = stored.Description;
    }

    private void Validate(Npc npc)
    {
        // Ein Angebot ohne Item ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (npc.IsTrader && npc.Offers.Any(offer => offer.ItemId == Guid.Empty))
        {
            throw new ContentValidationException(messages["TraderOfferItemRequired"]);
        }

        var duplicate = npc.Offers
            .GroupBy(offer => offer.ItemId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["TraderOfferDuplicate"]);
        }

        foreach (var offer in npc.Offers)
        {
            if (offer.SellPrice < 0 || offer.BuyPrice < 0)
            {
                throw new ContentValidationException(messages["TraderPriceNegative"]);
            }

            if (offer.Stock < 0)
            {
                throw new ContentValidationException(messages["TraderStockNegative"]);
            }

            if (offer.RestockSeconds < 0)
            {
                throw new ContentValidationException(messages["TraderRestockNegative"]);
            }

            if (offer.CurrencyId is null && (offer.SellPrice is not null || offer.BuyPrice is not null))
            {
                throw new ContentValidationException(messages["TraderPriceNeedsCurrency"]);
            }
        }

        // Eine Beziehung ohne Gegenseite oder Art ist eine unfertige Eingabezeile; die Maske
        // räumt sie vorher weg.
        if (npc.Relations.Any(relation => relation.OtherNpcId == Guid.Empty || relation.RelationTypeId == Guid.Empty))
        {
            throw new ContentValidationException(messages["NpcRelationIncomplete"]);
        }

        if (npc.Relations.Any(relation => relation.OtherNpcId == npc.Id))
        {
            throw new ContentValidationException(messages["NpcRelationSelf"]);
        }

        var duplicateRelation = npc.Relations
            .GroupBy(relation => (relation.OtherNpcId, relation.RelationTypeId))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateRelation is not null)
        {
            throw new ContentValidationException(messages["NpcRelationDuplicate"]);
        }
    }

    /// <summary>
    /// Gleicht die Spawn-Regeln ab — dasselbe Muster wie <see cref="SyncOffers"/>, samt
    /// EF-Fallstrick bei Kind-Sammlungen. Entfernte GUIDs kommen in dieselbe Liste: Ihre
    /// Bedingungen räumt derselbe Aufruf ab.
    /// </summary>
    private static void SyncSpawnRules(
        GameDevManagerDbContext db, Npc stored, Npc incoming, List<Guid> removedIds)
    {
        var wanted = incoming.SpawnRules;
        var wantedIds = wanted.Select(rule => rule.Id).ToHashSet();

        foreach (var obsolete in stored.SpawnRules.Where(r => !wantedIds.Contains(r.Id)).ToList())
        {
            stored.SpawnRules.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var rule = wanted[index];
            var target = stored.SpawnRules.FirstOrDefault(r => r.Id == rule.Id);

            // Mindestens einer, und die obere Grenze nie unter der unteren — eine verdrehte
            // Spanne ließe sich nicht auswürfeln.
            var min = Math.Max(1, rule.MinCount);
            var max = Math.Max(min, rule.MaxCount);

            // Eine Markierung ohne Karte wäre nicht zu deuten — dieselbe Regel wie beim
            // Schauplatz eines Story-Abschnitts.
            var markerId = rule.TargetMapId is null ? null : rule.TargetMarkerId;

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe SyncOffers.
                db.SpawnRules.Add(new SpawnRule
                {
                    Id = rule.Id,
                    NpcId = stored.Id,
                    TargetMapId = rule.TargetMapId,
                    TargetMarkerId = markerId,
                    MinCount = min,
                    MaxCount = max,
                    RespawnSeconds = rule.RespawnSeconds,
                    SortOrder = index
                });
            }
            else
            {
                target.TargetMapId = rule.TargetMapId;
                target.TargetMarkerId = markerId;
                target.MinCount = min;
                target.MaxCount = max;
                target.RespawnSeconds = rule.RespawnSeconds;
                target.SortOrder = index;
            }
        }
    }

    private static void SyncOffers(
        GameDevManagerDbContext db, Npc stored, Npc incoming, List<Guid> removedOfferIds)
    {
        var wanted = incoming.Offers;
        var wantedIds = wanted.Select(offer => offer.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Offers.Where(o => !wantedIds.Contains(o.Id)).ToList())
        {
            stored.Offers.Remove(obsolete);
            removedOfferIds.Add(obsolete.Id);
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

    private static void SyncRelations(GameDevManagerDbContext db, Npc stored, Npc incoming)
    {
        var wanted = incoming.Relations;
        var wantedIds = wanted.Select(relation => relation.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Relations.Where(r => !wantedIds.Contains(r.Id)).ToList())
        {
            stored.Relations.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var relation = wanted[index];
            var target = stored.Relations.FirstOrDefault(r => r.Id == relation.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe SyncOffers.
                db.NpcRelations.Add(new NpcRelation
                {
                    Id = relation.Id,
                    NpcId = stored.Id,
                    OtherNpcId = relation.OtherNpcId,
                    RelationTypeId = relation.RelationTypeId,
                    Stance = relation.Stance,
                    SortOrder = index
                });
            }
            else
            {
                target.OtherNpcId = relation.OtherNpcId;
                target.RelationTypeId = relation.RelationTypeId;
                target.Stance = relation.Stance;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>
    /// Die Beziehungen, die bei anderen NPCs gespeichert sind und auf diesen zeigen — die
    /// Maske zeigt sie mit der Gegenrichtungs-Bezeichnung („Berta ist Mutter von Anton“).
    /// </summary>
    public async Task<List<NpcRelationRow>> GetIncomingRelationsAsync(Guid npcId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.NpcRelations
            .AsNoTracking()
            .Where(relation => relation.OtherNpcId == npcId)
            .OrderBy(relation => relation.Npc!.Name)
            .Select(relation => new NpcRelationRow(
                relation.Id,
                relation.NpcId,
                relation.Npc!.Name,
                relation.RelationType!.InverseName,
                relation.Stance))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ Beziehungsarten

    public async Task<List<NpcRelationType>> GetRelationTypesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.NpcRelationTypes
            .AsNoTracking()
            .Where(type => type.GameProjectId == projectId)
            .OrderBy(type => type.Name)
            .ToListAsync(ct);
    }

    public async Task SaveRelationTypeAsync(NpcRelationType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type.Name) || string.IsNullOrWhiteSpace(type.InverseName))
        {
            throw new ContentValidationException(messages["NpcRelationTypeNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.NpcRelationTypes.FirstOrDefaultAsync(t => t.Id == type.Id, ct);

        if (stored is null)
        {
            stored = new NpcRelationType
            {
                Id = type.Id,
                GameProjectId = type.GameProjectId,
                Name = type.Name.Trim(),
                InverseName = type.InverseName.Trim()
            };

            db.NpcRelationTypes.Add(stored);
        }
        else
        {
            stored.Name = type.Name.Trim();
            stored.InverseName = type.InverseName.Trim();
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRelationTypeAsync(Guid typeId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete ohne vorheriges Speichern — hier greift der
        // WriteGuardInterceptor nicht, die Prüfung steht deshalb ausdrücklich da.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Verständliche Meldung statt des Restrict-Fremdschlüsselfehlers.
        var usage = await db.NpcRelations.CountAsync(relation => relation.RelationTypeId == typeId, ct);

        if (usage > 0)
        {
            throw new ContentValidationException(messages["NpcRelationTypeInUse", usage]);
        }

        await db.NpcRelationTypes.Where(type => type.Id == typeId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Löscht einen NPC mit Angebot, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteNpcAsync(Guid npcId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(npcId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Die Posten haben eigene GUIDs und können eigene Bedingungen tragen — sie müssen
        // deshalb mit aufgeräumt werden, bevor sie über den Fremdschlüssel verschwinden.
        var offerIds = await db.TraderOffers
            .Where(offer => offer.NpcId == npcId)
            .Select(offer => offer.Id)
            .ToListAsync(ct);

        // Spawn-Regeln tragen ihre Bedingungen unter der eigenen GUID.
        var spawnRuleIds = await db.SpawnRules
            .Where(rule => rule.NpcId == npcId)
            .Select(rule => rule.Id)
            .ToListAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Npcs, npcId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, db.Npcs, npcId, [.. offerIds, .. spawnRuleIds], ct);

        // Beziehungen, die bei anderen NPCs gespeichert sind und auf diesen zeigen, hängen
        // ohne Fremdschlüssel daran und blieben sonst als Waisen zurück.
        await db.NpcRelations
            .Where(relation => relation.OtherNpcId == npcId)
            .ExecuteDeleteAsync(ct);

        // Die Angebote und eigenen Beziehungen fallen über den Fremdschlüssel mit.
        await db.Npcs
            .Where(n => n.Id == npcId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
