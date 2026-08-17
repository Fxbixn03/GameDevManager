using System.Globalization;
using System.Text.Json;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Kennzahlen einer Zahlenspalte — die Grundlage der Balancing-Tabelle (F14).
/// <para>
/// Der <b>Mittelwert</b> ist die Zahl, gegen die man Ausreißer erkennt; Minimum und Maximum
/// spannen den Bereich auf, in dem sich die Werte bewegen sollten. Gezählt wird nur, was
/// gefüllt ist: Ein leeres Feld ist kein Wert von null, sondern eine offene Stelle.
/// </para>
/// </summary>
public sealed record ColumnStatistics(Guid FieldDefinitionId, int Count, double Average, double Min, double Max)
{
    /// <summary>
    /// Die Abweichung eines Wertes vom Mittelwert in Prozent. <c>null</c>, wenn der Mittelwert
    /// null ist — dann wäre jede Abweichung unendlich groß und die Auskunft wertlos.
    /// </summary>
    public double? DeviationOf(double value) =>
        Math.Abs(Average) < double.Epsilon ? null : (value - Average) / Math.Abs(Average) * 100d;
}

/// <summary>Eine gespeicherte Ansicht, wie die Oberfläche sie braucht.</summary>
public sealed record SavedViewRow(
    Guid Id, string ModuleKey, string Name, ContentFilter Filter, IReadOnlyList<Guid> ColumnFieldIds);

/// <summary>
/// Gefilterte Listenansichten und ihre gespeicherten Fassungen (F27).
/// <para>
/// <b>Eine Seite statt eines Filters in jeder Modul-Liste.</b> Die Listen sind je Modul eigen
/// gebaut — Kachelraster, Tabelle, Zeitstreifen —, und dieselbe Filterleiste zwanzigmal
/// nachzubauen hieße, sie zwanzigmal zu pflegen. Über die <see cref="IModuleEntitySource"/>
/// deckt eine Seite alle ab, auch die künftigen; dieselbe Überlegung wie bei der
/// Massenbearbeitung.
/// </para>
/// <para>
/// Gefiltert wird <b>in der Datenbank</b>, soweit es geht (Name, Art, Stand, Vorbild), und im
/// Speicher, wo es sein muss (Feldwerte, Tags, Sprites): Die hängen ohne Fremdschlüssel an der
/// GUID, und ein Join darauf wäre über alle vier Provider nicht gleich zu bekommen.
/// </para>
/// </summary>
public class SavedViewService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    ContentTypeService contentTypes,
    IChangeAuthorProvider author,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    private static readonly JsonSerializerOptions FilterJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ------------------------------------------------------------------------- Auswerten

    /// <summary>
    /// Wendet einen Filter auf ein Modul an und liefert die Zeilen samt den Werten der
    /// gewählten Spalten.
    /// </summary>
    public async Task<List<ContentRow>> QueryAsync(
        Guid projectId, string moduleKey, ContentFilter filter,
        IReadOnlyList<Guid> columnFieldIds, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(s => s.ModuleKey == moduleKey)
            ?? throw new ContentValidationException(messages["SavedViewModuleUnknown", moduleKey]);

        await using var db = await factory.CreateDbContextAsync(ct);

        await ExpandTypesAsync(filter, projectId, moduleKey, ct);

        var candidates = await source.QueryAsync(db, projectId, filter, ct);

        if (candidates.Count == 0)
        {
            return [];
        }

        var ids = candidates.Select(candidate => candidate.Id).ToList();

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.OwnerEntityId))
            .ToListAsync(ct);

        var byOwner = values.ToLookup(value => value.OwnerEntityId);

        var withSprite = filter.WithoutSprite
            ? await db.Assets
                .AsNoTracking()
                .Where(asset => asset.OwnerEntityId != null
                    && ids.Contains(asset.OwnerEntityId!.Value)
                    && asset.IsPrimary)
                .Select(asset => asset.OwnerEntityId!.Value)
                .ToListAsync(ct)
            : [];

        var tagged = filter.TagIds.Count > 0
            ? (await db.ContentTagAssignments
                .AsNoTracking()
                .Where(assignment => ids.Contains(assignment.TargetEntityId)
                    && filter.TagIds.Contains(assignment.ContentTagId))
                .ToListAsync(ct))
                .GroupBy(assignment => assignment.TargetEntityId)
                .ToDictionary(group => group.Key, group => group.Select(a => a.ContentTagId).ToHashSet())
            : [];

        var rows = new List<ContentRow>();

        foreach (var candidate in candidates)
        {
            // Sprite und Tags hängen ohne Fremdschlüssel an der GUID — im Speicher gefiltert,
            // nachdem die Datenbank die Menge schon eingeengt hat.
            if (filter.WithoutSprite && withSprite.Contains(candidate.Id))
            {
                continue;
            }

            if (filter.TagIds.Count > 0
                && (!tagged.TryGetValue(candidate.Id, out var own)
                    || !filter.TagIds.All(own.Contains)))
            {
                continue;
            }

            var ownValues = byOwner[candidate.Id].ToDictionary(value => value.FieldDefinitionId);

            if (!MatchesFields(filter, ownValues))
            {
                continue;
            }

            rows.Add(new ContentRow(
                candidate.Id,
                moduleKey,
                candidate.Name,
                candidate.Description,
                candidate.TypeName,
                candidate.Status,
                candidate.UpdatedAtUtc,
                columnFieldIds.Count == 0
                    ? ownValues
                    : ownValues
                        .Where(pair => columnFieldIds.Contains(pair.Key))
                        .ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        return rows;
    }

    /// <summary>
    /// Die Kennzahlen der Zahlenspalten über die gefundenen Zeilen — je Spalte Mittelwert,
    /// Minimum und Maximum.
    /// <para>
    /// Gerechnet wird über <b>genau die Zeilen, die dastehen</b>, und nicht über den ganzen
    /// Bestand: Wer nach „Waffen“ filtert, will den Durchschnitt der Waffen sehen und nicht
    /// den aller Items. Nur Zahlenfelder — bei einem Text gäbe es keinen Mittelwert.
    /// </para>
    /// </summary>
    public static List<ColumnStatistics> Summarize(
        IReadOnlyList<ContentRow> rows, IReadOnlyList<FieldDefinition> columns)
    {
        var statistics = new List<ColumnStatistics>();

        foreach (var column in columns.Where(column =>
            column.Type is ContentFieldType.Integer or ContentFieldType.Decimal or ContentFieldType.Formula))
        {
            var numbers = rows
                .Select(row => row.Values.TryGetValue(column.Id, out var value) ? value.NumberValue : null)
                .OfType<double>()
                .ToList();

            if (numbers.Count == 0)
            {
                continue;
            }

            statistics.Add(new ColumnStatistics(
                column.Id, numbers.Count, numbers.Average(), numbers.Min(), numbers.Max()));
        }

        return statistics;
    }

    /// <summary>
    /// Löst die gewählte Art in sie selbst und ihre Unterarten auf. Wer „Waffe“ filtert, meint
    /// fast immer auch „Nahkampf“ und „Fernkampf“ — sonst zeigte der Filter ausgerechnet den
    /// Bestand nicht, den die Feldvererbung zusammenhält.
    /// <para>
    /// Aufgelöst wird bei jeder Abfrage neu und nicht im gespeicherten Filter: Wer später eine
    /// Unterart anlegt, soll sie in seiner gespeicherten Ansicht wiederfinden, ohne sie neu zu
    /// wählen.
    /// </para>
    /// </summary>
    private async Task ExpandTypesAsync(
        ContentFilter filter, Guid projectId, string moduleKey, CancellationToken ct)
    {
        filter.ExpandedTypeIds = [];

        if (filter.ContentTypeId is not { } chosen || !filter.IncludeSubtypes)
        {
            return;
        }

        var types = await contentTypes.GetTypesAsync(projectId, moduleKey, ct);

        var wanted = new HashSet<Guid> { chosen };
        var grew = true;

        while (grew)
        {
            grew = false;

            foreach (var type in types.Where(type => type.ParentId is { } parent && wanted.Contains(parent)))
            {
                grew |= wanted.Add(type.Id);
            }
        }

        filter.ExpandedTypeIds = [.. wanted];
    }

    /// <summary>
    /// Prüft die Feldbedingungen. Im Speicher und nicht in SQL: Der Wert steht je nach Feldtyp
    /// in einer anderen Spalte, und die Zahl kommt als Text aus dem gespeicherten Filter — eine
    /// Übersetzung dafür wäre über vier Provider nicht gleich zu bekommen.
    /// </summary>
    private static bool MatchesFields(ContentFilter filter, Dictionary<Guid, FieldValue> values)
    {
        foreach (var criterion in filter.Fields)
        {
            values.TryGetValue(criterion.FieldDefinitionId, out var value);

            var empty = value is null || value.IsEmpty;

            var matches = criterion.Comparison switch
            {
                FieldComparison.IsEmpty => empty,
                FieldComparison.IsNotEmpty => !empty,
                _ when empty => false,
                FieldComparison.Contains => Text(value!).Contains(
                    criterion.Value ?? string.Empty, StringComparison.CurrentCultureIgnoreCase),
                FieldComparison.Equals => Text(value!).Equals(
                    criterion.Value?.Trim() ?? string.Empty, StringComparison.CurrentCultureIgnoreCase),
                FieldComparison.GreaterThan => Compare(value!, criterion.Value) > 0,
                FieldComparison.LessThan => Compare(value!, criterion.Value) < 0,
                _ => true
            };

            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Der Wert als Text, egal in welcher Spalte er steht.</summary>
    private static string Text(FieldValue value) =>
        value.TextValue
        ?? value.NumberValue?.ToString(CultureInfo.InvariantCulture)
        ?? value.BooleanValue?.ToString()
        ?? value.DateValue?.ToString("o", CultureInfo.InvariantCulture)
        ?? value.ReferenceValue?.ToString()
        ?? value.OptionId?.ToString()
        ?? string.Empty;

    /// <summary>
    /// Größer/kleiner nur, wo es eine Ordnung gibt: Zahlen und Daten. Alles andere fällt
    /// durch — „Name größer als“ wäre eine Auskunft, die niemand gesucht hat.
    /// </summary>
    private static int Compare(FieldValue value, string? other)
    {
        if (value.NumberValue is { } number
            && double.TryParse(other, NumberStyles.Any, CultureInfo.InvariantCulture, out var limit))
        {
            return number.CompareTo(limit);
        }

        if (value.DateValue is { } date && DateTime.TryParse(
                other, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var otherDate))
        {
            return date.CompareTo(otherDate);
        }

        return 0;
    }

    // ---------------------------------------------------------------- Ansichten verwalten

    /// <summary>Die gespeicherten Ansichten des angemeldeten Benutzers in diesem Projekt.</summary>
    public async Task<List<SavedViewRow>> GetViewsAsync(
        Guid projectId, string? moduleKey = null, CancellationToken ct = default)
    {
        var userId = (await author.GetCurrentAsync(ct)).UserId;

        if (userId is null)
        {
            // Ohne Anmeldung gibt es keinen Besitzer — und damit auch keine Ansichten.
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var views = await db.SavedViews
            .AsNoTracking()
            .Where(view => view.GameProjectId == projectId
                && view.AppUserId == userId
                && (moduleKey == null || view.ModuleKey == moduleKey))
            .OrderBy(view => view.ModuleKey).ThenBy(view => view.Name)
            .ToListAsync(ct);

        return [.. views.Select(Describe)];
    }

    private static SavedViewRow Describe(SavedView view) =>
        new(view.Id,
            view.ModuleKey,
            view.Name,
            JsonSerializer.Deserialize<ContentFilter>(view.FilterJson, FilterJson) ?? new ContentFilter(),
            GuidList.Parse(view.ColumnFieldIds));

    /// <summary>Legt eine Ansicht an oder schreibt sie fort. Der Name ist je Modul eindeutig.</summary>
    public async Task<Guid> SaveViewAsync(
        Guid projectId, Guid? viewId, string moduleKey, string name,
        ContentFilter filter, IReadOnlyList<Guid> columnFieldIds, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["SavedViewNameRequired"]);
        }

        var userId = (await author.GetCurrentAsync(ct)).UserId
            ?? throw new ContentValidationException(messages["SavedViewNeedsUser"]);

        name = name.Trim();

        await using var db = await factory.CreateDbContextAsync(ct);

        var taken = await db.SavedViews.AnyAsync(
            view => view.GameProjectId == projectId
                && view.AppUserId == userId
                && view.ModuleKey == moduleKey
                && view.Name == name
                && view.Id != viewId, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["SavedViewNameExists", name]);
        }

        var stored = viewId is { } id
            ? await db.SavedViews.FirstOrDefaultAsync(view => view.Id == id && view.AppUserId == userId, ct)
            : null;

        if (stored is null)
        {
            stored = new SavedView
            {
                GameProjectId = projectId,
                AppUserId = userId,
                ModuleKey = moduleKey,
                Name = name,
                FilterJson = string.Empty
            };

            db.SavedViews.Add(stored);
        }

        stored.Name = name;
        stored.ModuleKey = moduleKey;
        stored.FilterJson = JsonSerializer.Serialize(filter, FilterJson);
        stored.ColumnFieldIds = GuidList.Format(columnFieldIds);
        stored.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return stored.Id;
    }

    public async Task DeleteViewAsync(Guid viewId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        var userId = (await author.GetCurrentAsync(ct)).UserId;

        if (userId is null)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        // Nur die eigenen: Eine Ansicht ist eine Arbeitsgewohnheit, keine Projektangabe.
        await db.SavedViews
            .Where(view => view.Id == viewId && view.AppUserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
