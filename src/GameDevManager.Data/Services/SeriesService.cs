using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Serien-Anlage: „Lege 20 Eisenwaffen an“ ohne 20 Masken. Aus Art, Anzahl und einer
/// <see cref="NameTemplate"/>-Vorlage entstehen Entwürfe — Bearbeitungsstand
/// <see cref="ContentStatus.Draft"/>, damit Massenbearbeitung und gespeicherte Ansichten sie
/// sofort greifen.
/// <para>
/// Ein Lauf ist <b>ein Sammeleintrag</b> im Änderungsprotokoll, wie beim Import: 20 Zeilen
/// für 20 leere Entwürfe machten das Protokoll unlesbar (<c>SuppressChangeLog</c>). Der
/// Schreibschutz greift trotzdem — er hängt am <c>SaveChanges</c>, nicht am Protokoll.
/// </para>
/// </summary>
public class SeriesService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Obergrenze je Lauf. Wer wirklich tausend Entwürfe braucht, fährt fünf Läufe — ein
    /// vertipptes „2000“ soll den Bestand nicht fluten.
    /// </summary>
    public const int MaxCount = 200;

    /// <summary>
    /// Ergänzt <c>{n}</c>, wenn die Vorlage keinen Platzhalter trägt: Ohne ihn bekäme jede
    /// Position denselben Namen, und in keiner Liste wäre einer vom anderen zu unterscheiden.
    /// Auch die Vorschau der Maske läuft hierüber — sie zeigt, was wirklich entsteht.
    /// </summary>
    public static string EnsureNumbering(string template, int count) =>
        count > 1 && !NameTemplate.HasPlaceholder(template)
            ? template.TrimEnd() + " {n}"
            : template;

    /// <summary>Legt die Serie an und liefert die GUIDs der neuen Entwürfe.</summary>
    public async Task<List<Guid>> CreateAsync(
        Guid projectId,
        string moduleKey,
        Guid? contentTypeId,
        string template,
        int count,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ContentValidationException(messages["SeriesTemplateRequired"].Value);
        }

        if (count < 1 || count > MaxCount)
        {
            throw new ContentValidationException(messages["SeriesCountOutOfRange", MaxCount].Value);
        }

        // Über die Modul-Quellen wie überall — und mit derselben Ausnahme wie beim Kopieren:
        // Wo ein zweiter Datensatz derselben Sache ein Widerspruch wäre (Diplomatie), ist
        // eine Serie erst recht einer.
        var source = sources.FirstOrDefault(s => s.ModuleKey == moduleKey && s.CanDuplicate)
            ?? throw new ContentValidationException(messages["SeriesModuleUnknown"].Value);

        var effective = EnsureNumbering(template.Trim(), count);
        var names = NameTemplate.Expand(effective, count);

        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new ContentValidationException(messages["SeriesTemplateRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        if (contentTypeId is { } typeId
            && !await db.ContentTypes.AnyAsync(
                type => type.Id == typeId
                    && type.GameProjectId == projectId
                    && type.ModuleKey == moduleKey, ct))
        {
            throw new ContentValidationException(messages["SeriesTypeUnknown"].Value);
        }

        var projectName = await db.GameProjects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new ContentValidationException(messages["Export_ProjectMissing"].Value);

        // Ein Lauf ist ein Eintrag, keine zwanzig — wie beim Import.
        db.SuppressChangeLog = true;

        var now = DateTime.UtcNow;
        var ids = new List<Guid>(count);

        foreach (var name in names)
        {
            var entity = source.CreateNew(projectId, name.Trim());

            entity.ContentTypeId = contentTypeId;
            entity.Status = ContentStatus.Draft;
            entity.CreatedAtUtc = now;
            entity.UpdatedAtUtc = now;

            db.Add(entity);
            ids.Add(entity.Id);
        }

        await db.SaveChangesAsync(ct);

        // Der Sammeleintrag; den Benutzernamen trägt der Interceptor nach — er weiß, wer
        // gerade handelt. Erster und letzter Name sagen, was entstanden ist.
        await ChangeLog.RecordProjectActionAsync(
            db, projectId, projectName, ChangeAction.Created,
            messages["ChangeLog_SeriesCreated", count, moduleKey, names[0], names[^1]].Value, ct);

        return ids;
    }
}
