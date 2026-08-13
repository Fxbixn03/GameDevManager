using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// „Als Vorlage kopieren“ für jedes Modul. Wie Referenzansicht, Suche und Startscreen läuft
/// auch das über die <see cref="IModuleEntitySource"/>: Ein neues Modul bekommt das Kopieren
/// mit seiner Quelle, ohne dass hier etwas nachzutragen wäre.
/// </summary>
public class EntityDuplicationService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Obergrenze des Namens — dieselbe wie an der Spalte.</summary>
    private const int NameMaxLength = 200;

    /// <summary>
    /// Lässt sich in diesem Modul überhaupt kopieren? Die Oberfläche blendet den Knopf sonst
    /// aus, statt ihn beim Klick abzulehnen.
    /// </summary>
    public bool CanDuplicate(string moduleKey) =>
        sources.FirstOrDefault(source => source.ModuleKey == moduleKey)?.CanDuplicate ?? false;

    /// <summary>
    /// Legt die Kopie an und gibt ihre GUID zurück — die Oberfläche springt anschließend in
    /// deren Bearbeitungsmaske, denn kopiert wird, um gleich weiterzuarbeiten.
    /// </summary>
    public async Task<Guid> DuplicateAsync(
        Guid projectId, string moduleKey, Guid entityId, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(s => s.ModuleKey == moduleKey && s.CanDuplicate)
            ?? throw new ContentValidationException(messages["Duplicate_NotSupported"].Value);

        await using var db = await factory.CreateDbContextAsync(ct);

        var existing = await source.GetEntitiesAsync(db, projectId, ct);
        var original = existing.FirstOrDefault(entity => entity.Id == entityId)
            ?? throw new ContentValidationException(messages["Duplicate_Missing"].Value);

        var copyId = await source.DuplicateAsync(db, entityId, FreeName(original.Name, existing), ct)
            ?? throw new ContentValidationException(messages["Duplicate_Missing"].Value);

        await db.SaveChangesAsync(ct);

        return copyId;
    }

    /// <summary>
    /// „Trank (Kopie)“, beim nächsten Mal „Trank (Kopie 2)“. Manche Module verlangen eindeutige
    /// Namen (Währungen), und selbst wo nicht, sind zwei gleichnamige Einträge in der Liste
    /// nicht auseinanderzuhalten.
    /// </summary>
    private string FreeName(string name, List<EntitySummary> existing)
    {
        var taken = existing
            .Select(entity => entity.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        // Kürzen, bevor der Zusatz drankommt: Ein Name an der Obergrenze darf die Kopie nicht
        // an der Spaltenlänge scheitern lassen.
        var suffix = messages["Duplicate_NameSuffix", string.Empty].Value;
        var stem = name.Length + suffix.Length > NameMaxLength
            ? name[..(NameMaxLength - suffix.Length - 4)].TrimEnd()
            : name;

        var candidate = messages["Duplicate_NameSuffix", stem].Value;

        for (var counter = 2; taken.Contains(candidate); counter++)
        {
            candidate = messages["Duplicate_NameSuffixNumbered", stem, counter].Value;
        }

        return candidate;
    }
}
