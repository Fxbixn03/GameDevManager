using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Cutscenes samt Storyboard und benutzerdefinierten Feldwerten.
/// </summary>
public class CutsceneService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets)
{
    public async Task<List<CutsceneListRow>> GetCutscenesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Cutscenes
            .AsNoTracking()
            .Where(c => c.GameProjectId == projectId)
            .OrderBy(c => c.Name)
            .Select(c => new CutsceneListRow(
                c.Id,
                c.Name,
                c.Description,
                c.Shots.Count,
                c.StoryEntryId,
                db.StoryEntries.Where(s => s.Id == c.StoryEntryId).Select(s => s.Name).FirstOrDefault(),
                c.ContentTypeId,
                c.ContentType!.Name,
                c.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == c.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Cutscene>?> LoadForEditAsync(
        Guid projectId, Guid? cutsceneId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Cutscenes, ct);

        if (cutsceneId is null)
        {
            return new ContentEditContext<Cutscene>
            {
                Entity = new Cutscene { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var cutscene = await db.Cutscenes
            .AsNoTracking()
            .Include(c => c.Shots)
            .FirstOrDefaultAsync(c => c.Id == cutsceneId && c.GameProjectId == projectId, ct);

        if (cutscene is null)
        {
            return null;
        }

        cutscene.Shots = [.. cutscene.Shots.OrderBy(s => s.SortOrder)];

        return new ContentEditContext<Cutscene>
        {
            Entity = cutscene,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, cutscene.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, cutscene.Id, ct)
        };
    }

    public async Task SaveCutsceneAsync(ContentEditContext<Cutscene> context, CancellationToken ct = default)
    {
        var cutscene = context.Entity;

        if (string.IsNullOrWhiteSpace(cutscene.Name))
        {
            throw new ContentValidationException("Die Cutscene braucht einen Namen.");
        }

        // Eine Einstellung ohne Text ist eine unfertige Eingabezeile; die Maske räumt sie vorher weg.
        if (cutscene.Shots.Any(shot => string.IsNullOrWhiteSpace(shot.Text)))
        {
            throw new ContentValidationException("Jede Einstellung braucht einen Text.");
        }

        ContentFields.ValidateRequired(context);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Cutscenes
            .Include(c => c.Shots)
            .FirstOrDefaultAsync(c => c.Id == cutscene.Id, ct);

        if (stored is null)
        {
            stored = new Cutscene
            {
                Id = cutscene.Id,
                GameProjectId = cutscene.GameProjectId,
                Name = cutscene.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Cutscenes.Add(stored);
        }

        stored.ContentTypeId = cutscene.ContentTypeId;
        stored.Name = cutscene.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(cutscene.Description) ? null : cutscene.Description.Trim();
        stored.StoryEntryId = cutscene.StoryEntryId;
        stored.DialogueId = cutscene.DialogueId;
        stored.UpdatedAtUtc = now;

        var removedShotIds = new List<Guid>();
        SyncShots(db, stored, cutscene, removedShotIds);

        await EntityCleanup.DeleteForEntitiesAsync(db, removedShotIds, ct);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        cutscene.CreatedAtUtc = stored.CreatedAtUtc;
        cutscene.UpdatedAtUtc = stored.UpdatedAtUtc;
        cutscene.Name = stored.Name;
        cutscene.Description = stored.Description;
    }

    private static void SyncShots(
        GameDevManagerDbContext db, Cutscene stored, Cutscene incoming, List<Guid> removedIds)
    {
        var wanted = incoming.Shots;
        var wantedIds = wanted.Select(shot => shot.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen — der Fremdschlüssel ist pflicht, EF löscht
        // die Waise dadurch von selbst.
        foreach (var obsolete in stored.Shots.Where(s => !wantedIds.Contains(s.Id)).ToList())
        {
            stored.Shots.Remove(obsolete);
            removedIds.Add(obsolete.Id);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var shot = wanted[index];
            var target = stored.Shots.FirstOrDefault(s => s.Id == shot.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
                db.CutsceneShots.Add(new CutsceneShot
                {
                    Id = shot.Id,
                    CutsceneId = stored.Id,
                    Text = shot.Text.Trim(),
                    SortOrder = index
                });
            }
            else
            {
                target.Text = shot.Text.Trim();
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht eine Cutscene mit Storyboard, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteCutsceneAsync(Guid cutsceneId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(cutsceneId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var shotIds = await db.CutsceneShots
            .Where(shot => shot.CutsceneId == cutsceneId)
            .Select(shot => shot.Id)
            .ToListAsync(ct);

        await EntityCleanup.DeleteForEntitiesAsync(db, [cutsceneId, .. shotIds], ct);

        // Das Storyboard fällt über den Fremdschlüssel mit.
        await db.Cutscenes
            .Where(c => c.Id == cutsceneId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
