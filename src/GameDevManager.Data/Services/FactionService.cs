using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Fraktionen samt Mitgliederliste und benutzerdefinierten Feldwerten.
/// </summary>
public class FactionService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<FactionListRow>> GetFactionsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Factions
            .AsNoTracking()
            .Where(f => f.GameProjectId == projectId)
            .OrderBy(f => f.Name)
            .Select(f => new FactionListRow(
                f.Id,
                f.Name,
                f.Description,
                f.ContentTypeId,
                f.ContentType!.Name,
                f.Members.Count,
                f.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == f.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Fraktionen, in denen ein NPC Mitglied ist — im Konzept: „Dies wird dann auch zu
    /// einem NPC in dem NPC-Modul angezeigt.“
    /// </summary>
    public async Task<List<FactionForNpc>> GetFactionsForNpcAsync(
        Guid projectId, Guid npcId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.FactionMembers
            .AsNoTracking()
            .Where(m => m.NpcId == npcId && m.Faction!.GameProjectId == projectId)
            .OrderBy(m => m.Faction!.Name)
            .Select(m => new FactionForNpc(m.FactionId, m.Faction!.Name, m.Role))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Faction>?> LoadForEditAsync(
        Guid projectId, Guid? factionId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Factions, ct);

        if (factionId is null)
        {
            return new ContentEditContext<Faction>
            {
                Entity = new Faction { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var faction = await db.Factions
            .AsNoTracking()
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == factionId && f.GameProjectId == projectId, ct);

        if (faction is null)
        {
            return null;
        }

        faction.Members = [.. faction.Members.OrderBy(m => m.SortOrder)];

        return new ContentEditContext<Faction>
        {
            Entity = faction,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, faction.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, faction.Id, ct)
        };
    }

    public async Task SaveFactionAsync(ContentEditContext<Faction> context, CancellationToken ct = default)
    {
        var faction = context.Entity;

        if (string.IsNullOrWhiteSpace(faction.Name))
        {
            throw new ContentValidationException(messages["FactionNameRequired"]);
        }

        Validate(faction);
        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Factions
            .Include(f => f.Members)
            .FirstOrDefaultAsync(f => f.Id == faction.Id, ct);

        if (stored is null)
        {
            stored = new Faction
            {
                Id = faction.Id,
                GameProjectId = faction.GameProjectId,
                Name = faction.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Factions.Add(stored);
        }

        stored.ContentTypeId = faction.ContentTypeId;
        stored.Name = faction.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(faction.Description) ? null : faction.Description.Trim();
        stored.UpdatedAtUtc = now;

        var removedMemberIds = new List<Guid>();
        SyncMembers(db, stored, faction, removedMemberIds);

        // Falls an entfernten Mitgliedschaften etwas über deren GUID hängt, fällt es nicht von selbst mit.
        await EntityCleanup.DeleteForEntitiesAsync(db, removedMemberIds, ct);

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        faction.CreatedAtUtc = stored.CreatedAtUtc;
        faction.UpdatedAtUtc = stored.UpdatedAtUtc;
        faction.Name = stored.Name;
        faction.Description = stored.Description;
    }

    private void Validate(Faction faction)
    {
        // Eine Mitgliedschaft ohne NPC ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (faction.Members.Any(member => member.NpcId == Guid.Empty))
        {
            throw new ContentValidationException(messages["FactionMemberNpcRequired"]);
        }

        var duplicate = faction.Members
            .GroupBy(member => member.NpcId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["FactionMemberDuplicate"]);
        }
    }

    private static void SyncMembers(
        GameDevManagerDbContext db, Faction stored, Faction incoming, List<Guid> removedMemberIds)
    {
        var wanted = incoming.Members;
        var wantedIds = wanted.Select(member => member.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Members.Where(m => !wantedIds.Contains(m.Id)).ToList())
        {
            stored.Members.Remove(obsolete);
            removedMemberIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var member = wanted[index];
            var target = stored.Members.FirstOrDefault(m => m.Id == member.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet: die Mitgliedschaft bringt ihre GUID schon mit,
                // und EF hielte sie beim Anhängen an eine bestehende Fraktion sonst für einen
                // vorhandenen Datensatz — es entstünde ein UPDATE auf eine Zeile, die es noch nicht gibt.
                db.FactionMembers.Add(new FactionMember
                {
                    Id = member.Id,
                    FactionId = stored.Id,
                    NpcId = member.NpcId,
                    Role = NormalizeRole(member.Role),
                    SortOrder = index
                });
            }
            else
            {
                target.NpcId = member.NpcId;
                target.Role = NormalizeRole(member.Role);
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht eine Fraktion mit Mitgliedern, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteFactionAsync(Guid factionId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(factionId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Mitgliedschaften haben eigene GUIDs — was über diese an ihnen hängt, muss mit weg.
        var memberIds = await db.FactionMembers
            .Where(member => member.FactionId == factionId)
            .Select(member => member.Id)
            .ToListAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Factions, factionId, ct);
        await EntityCleanup.DeleteForEntitiesAsync(db, [factionId, .. memberIds], ct);

        // Die Mitgliedschaften fallen über den Fremdschlüssel mit.
        await db.Factions
            .Where(f => f.Id == factionId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? NormalizeRole(string? role) =>
        string.IsNullOrWhiteSpace(role) ? null : role.Trim();
}
