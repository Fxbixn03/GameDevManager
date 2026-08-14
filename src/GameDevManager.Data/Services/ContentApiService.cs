using System.Text.Json;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Leseseite der HTTP-API: dieselben Daten wie im Export, nur ohne den Umweg über das ZIP.
/// Gedacht für ein Editor-Plugin in Unity oder Godot, das den Stand direkt zieht.
/// <para>
/// <b>Nur lesend</b>, und zwar bewusst: Ein Schlüssel, der auch schreiben dürfte, wäre ein
/// zweiter Weg an Rechteprüfung, Änderungsprotokoll und Schreibkonflikt-Erkennung vorbei. Wer
/// schreiben will, meldet sich an.
/// </para>
/// <para>
/// Serialisiert wird mit denselben Regeln wie der Export (<see cref="JsonOptions"/>) —
/// Navigationsobjekte fallen weg, Referenzen bleiben GUIDs. Ein Plugin, das das ZIP lesen
/// kann, kann damit auch die API lesen.
/// </para>
/// </summary>
public class ContentApiService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources)
{
    /// <summary>Die JSON-Regeln des Exports, damit die Endpunkte sie mitbenutzen können.</summary>
    public static JsonSerializerOptions JsonOptions => ExportFormat.JsonOptions;

    /// <summary>Die Projekte der Installation — der Einstieg für ein Plugin.</summary>
    public async Task<List<object>> GetProjectsAsync(Guid? onlyProject, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db.GameProjects.AsNoTracking();

        if (onlyProject is { } id)
        {
            query = query.Where(project => project.Id == id);
        }

        return await query
            .OrderBy(project => project.Name)
            .Select(project => (object)new
            {
                project.Id,
                project.Name,
                project.Description,
                project.CreatedAtUtc
            })
            .ToListAsync(ct);
    }

    /// <summary>Welche Module überhaupt Inhalt tragen — die Wegweiser der API.</summary>
    public IReadOnlyList<string> ModuleKeys => [.. sources.Select(source => source.ModuleKey).Order()];

    /// <summary>
    /// Ein ganzes Modul: seine Arten samt Feldern, seine Einträge und deren Feldwerte.
    /// <c>null</c>, wenn es das Modul nicht gibt.
    /// </summary>
    public async Task<object?> GetModuleAsync(
        Guid projectId, string moduleKey, string? languageCode, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(entry => entry.ModuleKey == moduleKey);
        if (source is null)
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var entities = await source.LoadAllAsync(db, projectId, ct);
        var ids = entities.Select(entity => entity.Id).ToList();

        var contentTypes = await db.ContentTypes
            .AsNoTracking()
            .Include(type => type.Fields).ThenInclude(field => field.Options)
            .Where(type => type.GameProjectId == projectId && type.ModuleKey == moduleKey)
            .OrderBy(type => type.SortOrder).ThenBy(type => type.Name)
            .ToListAsync(ct);

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.OwnerEntityId))
            .OrderBy(value => value.OwnerEntityId).ThenBy(value => value.FieldDefinitionId)
            .ToListAsync(ct);

        // Eine Sprache liefert die Texte gleich mit übersetzt — sonst müsste das Plugin die
        // Zeichenketten-Tabelle selbst dazuladen und zusammenführen.
        var translations = languageCode is null
            ? []
            : await db.ContentTranslations
                .AsNoTracking()
                .Where(t => t.GameProjectId == projectId
                    && t.LanguageCode == languageCode
                    && ids.Contains(t.OwnerEntityId))
                .OrderBy(t => t.OwnerEntityId).ThenBy(t => t.Slot)
                .Select(t => new { t.OwnerEntityId, t.Slot, t.Text })
                .ToListAsync(ct);

        return new
        {
            moduleKey,
            contentTypes,
            entities,
            fieldValues = values,
            language = languageCode,
            translations
        };
    }

    /// <summary>Ein einzelner Eintrag samt seiner Feldwerte — <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<object?> GetEntityAsync(
        Guid projectId, string moduleKey, Guid entityId, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(entry => entry.ModuleKey == moduleKey);
        if (source is null)
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var entity = (await source.LoadForBulkAsync(db, projectId, [entityId], ct)).FirstOrDefault();
        if (entity is null)
        {
            return null;
        }

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => value.OwnerEntityId == entityId)
            .OrderBy(value => value.FieldDefinitionId)
            .ToListAsync(ct);

        return new { moduleKey, entity, fieldValues = values };
    }
}
