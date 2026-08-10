using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Das Tag-Modul: modulübergreifende Tags definieren, je Modul freigeben und Entitäten
/// zuweisen. Die Asset-Stichwörter der Sprite-Bibliothek bleiben davon getrennt.
/// </summary>
public class TagService(IDbContextFactory<GameDevManagerDbContext> factory, IStringLocalizer<DataMessages> messages)
{
    public async Task<List<ContentTagRow>> GetTagsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentTags
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name)
            .Select(t => new ContentTagRow(
                t.Id,
                t.Name,
                t.Description,
                t.Color,
                t.Scopes.Select(s => s.ModuleKey).ToList(),
                t.Assignments.Count))
            .ToListAsync(ct);
    }

    public async Task SaveTagAsync(
        Guid projectId, Guid tagId, string name, string? description, string? color,
        IReadOnlyCollection<string> moduleKeys, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["TagNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var trimmed = name.Trim();
        var taken = await db.ContentTags.AnyAsync(
            other => other.GameProjectId == projectId && other.Name == trimmed && other.Id != tagId, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["TagNameExists", trimmed]);
        }

        var stored = await db.ContentTags
            .Include(t => t.Scopes)
            .FirstOrDefaultAsync(t => t.Id == tagId, ct);

        if (stored is null)
        {
            stored = new ContentTag
            {
                Id = tagId,
                GameProjectId = projectId,
                Name = trimmed
            };

            db.ContentTags.Add(stored);
        }

        stored.Name = trimmed;
        stored.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        stored.Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();

        // Freigaben abgleichen: fehlende ergänzen, überzählige entfernen.
        var wanted = moduleKeys.Distinct().ToHashSet();

        foreach (var obsolete in stored.Scopes.Where(s => !wanted.Contains(s.ModuleKey)).ToList())
        {
            stored.Scopes.Remove(obsolete);
        }

        foreach (var moduleKey in wanted.Where(key => stored.Scopes.All(s => s.ModuleKey != key)))
        {
            // Ausdrücklich über das DbSet — siehe EF-Fallstrick bei Kind-Sammlungen.
            db.ContentTagScopes.Add(new ContentTagScope
            {
                ContentTagId = stored.Id,
                ModuleKey = moduleKey
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht ein Tag; Freigaben und Zuweisungen fallen über den Fremdschlüssel mit.</summary>
    public async Task DeleteTagAsync(Guid tagId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.ContentTags
            .Where(t => t.Id == tagId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>Die Tags einer Entität, alphabetisch.</summary>
    public async Task<List<ContentTagRow>> GetTagsForEntityAsync(
        Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentTagAssignments
            .AsNoTracking()
            .Where(a => a.TargetEntityId == entityId)
            .OrderBy(a => a.ContentTag!.Name)
            .Select(a => new ContentTagRow(
                a.ContentTagId,
                a.ContentTag!.Name,
                a.ContentTag.Description,
                a.ContentTag.Color,
                a.ContentTag.Scopes.Select(s => s.ModuleKey).ToList(),
                a.ContentTag.Assignments.Count))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Die Tags, die in einem Modul vergeben werden dürfen: alle ohne Freigabe-Einschränkung
    /// plus die ausdrücklich für dieses Modul freigegebenen.
    /// </summary>
    public async Task<List<ContentTagRow>> GetAssignableTagsAsync(
        Guid projectId, string moduleKey, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentTags
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId
                && (!t.Scopes.Any() || t.Scopes.Any(s => s.ModuleKey == moduleKey)))
            .OrderBy(t => t.Name)
            .Select(t => new ContentTagRow(
                t.Id,
                t.Name,
                t.Description,
                t.Color,
                t.Scopes.Select(s => s.ModuleKey).ToList(),
                t.Assignments.Count))
            .ToListAsync(ct);
    }

    public async Task AssignAsync(
        Guid tagId, string moduleKey, Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var exists = await db.ContentTagAssignments
            .AnyAsync(a => a.ContentTagId == tagId && a.TargetEntityId == entityId, ct);

        if (exists)
        {
            return;
        }

        db.ContentTagAssignments.Add(new ContentTagAssignment
        {
            ContentTagId = tagId,
            TargetModuleKey = moduleKey,
            TargetEntityId = entityId
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task UnassignAsync(Guid tagId, Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.ContentTagAssignments
            .Where(a => a.ContentTagId == tagId && a.TargetEntityId == entityId)
            .ExecuteDeleteAsync(ct);
    }
}
