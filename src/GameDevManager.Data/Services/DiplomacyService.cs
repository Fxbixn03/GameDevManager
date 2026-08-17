using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der diplomatischen Beziehungen zwischen Fraktionen samt
/// benutzerdefinierten Feldwerten — und die Daten für die Graph-Ansicht des Konzepts.
/// </summary>
public class DiplomacyService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<DiplomacyListRow>> GetRelationsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.DiplomaticRelations
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId)
            .OrderBy(r => r.Name)
            .Select(r => new DiplomacyListRow(
                r.Id,
                r.Name,
                r.Description,
                r.Stance,
                r.FactionAId,
                db.Factions.Where(f => f.Id == r.FactionAId).Select(f => f.Name).FirstOrDefault(),
                r.FactionBId,
                db.Factions.Where(f => f.Id == r.FactionBId).Select(f => f.Name).FirstOrDefault(),
                r.ContentTypeId,
                r.ContentType!.Name,
                r.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Alles für die Graph-Ansicht in einem Zug: sämtliche Fraktionen als Knoten und die
    /// Beziehungen als Kanten. Fraktionen ohne Beziehung erscheinen bewusst mit — sonst
    /// fiele nicht auf, dass eine Fraktion diplomatisch noch in der Luft hängt.
    /// </summary>
    public async Task<DiplomacyGraph> GetGraphAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var factions = await db.Factions
            .AsNoTracking()
            .Where(f => f.GameProjectId == projectId)
            .OrderBy(f => f.Name)
            .Select(f => new DiplomacyGraphNode(f.Id, f.Name))
            .ToListAsync(ct);

        var relations = await db.DiplomaticRelations
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId)
            .Select(r => new DiplomacyGraphEdge(r.Id, r.Name, r.FactionAId, r.FactionBId, r.Stance))
            .ToListAsync(ct);

        return new DiplomacyGraph(factions, relations);
    }

    public async Task<ContentEditContext<DiplomaticRelation>?> LoadForEditAsync(
        Guid projectId, Guid? relationId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Diplomacy, ct);

        if (relationId is null)
        {
            return new ContentEditContext<DiplomaticRelation>
            {
                Entity = new DiplomaticRelation { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var relation = await db.DiplomaticRelations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == relationId && r.GameProjectId == projectId, ct);

        if (relation is null)
        {
            return null;
        }

        return new ContentEditContext<DiplomaticRelation>
        {
            Entity = relation,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, relation.Id, ct),
            Values = await ContentFields.LoadValuesAsync<DiplomaticRelation>(db, relation.Id, ct)
        };
    }

    public async Task SaveRelationAsync(
        ContentEditContext<DiplomaticRelation> context, CancellationToken ct = default)
    {
        var relation = context.Entity;

        if (string.IsNullOrWhiteSpace(relation.Name))
        {
            throw new ContentValidationException(messages["RelationNameRequired"]);
        }

        if (relation.FactionAId == Guid.Empty || relation.FactionBId == Guid.Empty)
        {
            throw new ContentValidationException(messages["RelationNeedsTwoFactions"]);
        }

        if (relation.FactionAId == relation.FactionBId)
        {
            throw new ContentValidationException(messages["RelationSelfReference"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Die Beziehung ist ungerichtet — dasselbe Paar in beliebiger Reihenfolge zählt als Duplikat.
        var duplicate = await db.DiplomaticRelations.AnyAsync(
            other => other.GameProjectId == relation.GameProjectId
                && other.Id != relation.Id
                && ((other.FactionAId == relation.FactionAId && other.FactionBId == relation.FactionBId)
                    || (other.FactionAId == relation.FactionBId && other.FactionBId == relation.FactionAId)), ct);

        if (duplicate)
        {
            throw new ContentValidationException(messages["RelationDuplicate"]);
        }

        var now = DateTime.UtcNow;
        var stored = await db.DiplomaticRelations.FirstOrDefaultAsync(r => r.Id == relation.Id, ct);

        if (stored is null)
        {
            stored = new DiplomaticRelation
            {
                Id = relation.Id,
                GameProjectId = relation.GameProjectId,
                Name = relation.Name.Trim(),
                CreatedAtUtc = now
            };

            db.DiplomaticRelations.Add(stored);
        }

        stored.ContentTypeId = relation.ContentTypeId;
        stored.Name = relation.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(relation.Description) ? null : relation.Description.Trim();
        stored.FactionAId = relation.FactionAId;
        stored.FactionBId = relation.FactionBId;
        stored.Stance = relation.Stance;
        stored.UpdatedAtUtc = now;

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        relation.CreatedAtUtc = stored.CreatedAtUtc;
        relation.UpdatedAtUtc = stored.UpdatedAtUtc;
        relation.Name = stored.Name;
        relation.Description = stored.Description;
    }

    /// <summary>Löscht eine Beziehung mit ihren Werten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteRelationAsync(Guid relationId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(relationId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.DiplomaticRelations, relationId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, db.DiplomaticRelations, relationId, null, ct);

        await db.DiplomaticRelations
            .Where(r => r.Id == relationId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    /// <summary>Anzeigename einer Haltung — an einer Stelle, damit Liste, Graph und Maske gleich sprechen.</summary>
    public string StanceLabel(DiplomaticStance stance) => stance switch
    {
        DiplomaticStance.Alliance => messages["Stance_Alliance"],
        DiplomaticStance.Friendship => messages["Stance_Friendship"],
        DiplomaticStance.Neutral => messages["Stance_Neutral"],
        DiplomaticStance.Hostility => messages["Stance_Hostility"],
        DiplomaticStance.War => messages["Stance_War"],
        _ => stance.ToString()
    };
}
