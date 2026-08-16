using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Benannte Exporte je Projekt: „Unity, nur Fertiges, ohne Werkzeug-Module“.
/// <para>
/// Ein Profil ändert nichts am Export selbst — es merkt sich nur die Schalter, die sonst jedes
/// Mal von Hand gesetzt werden. Genau deshalb ist es auch die Voraussetzung für einen
/// Zeitplan, der etwas ausführen soll (siehe F39 in der ToDo).
/// </para>
/// </summary>
public class ExportProfileService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<ExportProfile>> GetProfilesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ExportProfiles
            .AsNoTracking()
            .Where(profile => profile.GameProjectId == projectId)
            .OrderBy(profile => profile.SortOrder)
            .ThenBy(profile => profile.Name)
            .ToListAsync(ct);
    }

    public async Task SaveProfileAsync(
        Guid projectId, ExportProfile profile, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ContentValidationException(messages["ExportProfile_NameRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var name = profile.Name.Trim();
        var taken = await db.ExportProfiles.AnyAsync(
            other => other.GameProjectId == projectId && other.Name == name && other.Id != profile.Id, ct);

        // Zwei gleichnamige Profile wären in der Auswahl nicht auseinanderzuhalten — dieselbe
        // Überlegung wie bei den Währungen.
        if (taken)
        {
            throw new ContentValidationException(messages["ExportProfile_NameExists", name].Value);
        }

        var stored = await db.ExportProfiles.FirstOrDefaultAsync(p => p.Id == profile.Id, ct);

        if (stored is null)
        {
            stored = new ExportProfile { Id = profile.Id, GameProjectId = projectId, Name = name };
            db.ExportProfiles.Add(stored);
        }

        stored.Name = name;
        stored.Target = profile.Target;
        stored.IncludeAssets = profile.IncludeAssets;
        stored.MinimumStatus = profile.MinimumStatus;
        stored.ModuleKeys = Normalize(profile.ModuleKeys);
        stored.SortOrder = profile.SortOrder;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteProfileAsync(Guid profileId, CancellationToken ct = default)
    {
        // Reiner ExecuteDelete-Pfad ohne vorheriges Speichern — die Prüfung steht hier.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.ExportProfiles.Where(profile => profile.Id == profileId).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Eine leere Auswahl heißt „alle Module“ und nicht „keines“: Ein Profil ohne Häkchen wäre
    /// ein Export, der nichts enthält — das will niemand speichern, und als Vorgabe ist „alles“
    /// die einzige sinnvolle Lesart.
    /// </summary>
    private static string? Normalize(string? moduleKeys) =>
        string.IsNullOrWhiteSpace(moduleKeys) ? null : moduleKeys.Trim();
}
