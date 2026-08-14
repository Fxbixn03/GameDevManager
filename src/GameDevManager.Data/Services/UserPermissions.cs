using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Was der handelnde Benutzer darf — die Momentaufnahme, gegen die Dienste und Interceptor
/// prüfen. <c>null</c> bei <see cref="AllowedModules"/> heißt: alle Module.
/// <para>
/// Verwalter haben immer alle Rechte; das löst <see cref="For"/> auf, damit die Regel an
/// genau einer Stelle steht und nicht jede Prüfung an den Verwalterstatus denken muss.
/// </para>
/// </summary>
public sealed record UserPermissions(
    bool IsAdministrator,
    bool CanWrite,
    bool CanExport,
    bool CanImport,
    IReadOnlySet<string>? AllowedModules)
{
    /// <summary>
    /// Alles erlaubt — der Zustand ohne Anmeldung (Anwendungsstart, Ersteinrichtung, Tests).
    /// Dieselbe Überlegung wie beim Systemnamen des Änderungsprotokolls: Wo niemand angemeldet
    /// ist, geschieht in der Oberfläche nichts, das einzuschränken wäre.
    /// </summary>
    public static UserPermissions Full { get; } = new(true, true, true, true, null);

    /// <summary>Löst die gespeicherten Spalten eines Benutzers auf — Verwalter bekommen alles.</summary>
    public static UserPermissions For(
        bool isAdministrator, bool canWrite, bool canExport, bool canImport, string? allowedModuleKeys) =>
        isAdministrator
            ? Full
            : new UserPermissions(false, canWrite, canExport, canImport, ParseModuleKeys(allowedModuleKeys));

    public bool CanAccessModule(string moduleKey) =>
        AllowedModules is null || AllowedModules.Contains(moduleKey);

    /// <summary>Die kommagetrennte Spalte als Menge — <c>null</c> und Leeres heißen: alle.</summary>
    public static IReadOnlySet<string>? ParseModuleKeys(string? allowedModuleKeys)
    {
        if (string.IsNullOrWhiteSpace(allowedModuleKeys))
        {
            return null;
        }

        var keys = allowedModuleKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(key => key.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return keys.Count == 0 ? null : keys;
    }

    /// <summary>Das Gegenstück für das Speichern — stabil sortiert, <c>null</c> für „alle“.</summary>
    public static string? FormatModuleKeys(IEnumerable<string>? moduleKeys)
    {
        if (moduleKeys is null)
        {
            return null;
        }

        var keys = moduleKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        return keys.Count == 0 ? null : string.Join(",", keys);
    }
}

/// <summary>
/// Woher die Datenschicht erfährt, was der handelnde Benutzer darf — dasselbe Muster wie
/// <see cref="IChangeAuthorProvider"/>: Die Antwort liegt in der Web-Schicht (den Ansprüchen
/// des Anmelde-Cookies), gebraucht wird sie aber beim Speichern und in den Diensten.
/// </summary>
public interface IUserPermissionsProvider
{
    ValueTask<UserPermissions> GetCurrentAsync(CancellationToken ct = default);
}

/// <summary>Die Vorgabe für alles, was ohne Anmeldung läuft — Wartung, Tests, der erste Start.</summary>
public sealed class FullUserPermissionsProvider : IUserPermissionsProvider
{
    public ValueTask<UserPermissions> GetCurrentAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(UserPermissions.Full);
}

/// <summary>
/// Die Prüfungen, die Dienste vor besonderen Vorgängen aufrufen. Gewöhnliches Schreiben
/// braucht keinen Aufruf — das fängt der <see cref="WriteGuardInterceptor"/> zentral am
/// <c>SaveChanges</c> ab. Hier stehen die Wege daran vorbei: reine <c>ExecuteDelete</c>-Pfade
/// ohne vorheriges Speichern, Dateisystem-Vorgänge (Exportstände) und Import/Export.
/// </summary>
public class PermissionGuard(IUserPermissionsProvider permissions, IStringLocalizer<DataMessages> messages)
{
    public ValueTask<UserPermissions> GetCurrentAsync(CancellationToken ct = default) =>
        permissions.GetCurrentAsync(ct);

    public async Task EnsureCanWriteAsync(CancellationToken ct = default)
    {
        if (!(await permissions.GetCurrentAsync(ct)).CanWrite)
        {
            throw new ContentValidationException(messages["PermissionWriteDenied"]);
        }
    }

    public async Task EnsureCanExportAsync(CancellationToken ct = default)
    {
        if (!(await permissions.GetCurrentAsync(ct)).CanExport)
        {
            throw new ContentValidationException(messages["PermissionExportDenied"]);
        }
    }

    /// <summary>Ein Import schreibt den ganzen Bestand — er braucht beide Rechte.</summary>
    public async Task EnsureCanImportAsync(CancellationToken ct = default)
    {
        var current = await permissions.GetCurrentAsync(ct);

        if (!current.CanImport)
        {
            throw new ContentValidationException(messages["PermissionImportDenied"]);
        }

        if (!current.CanWrite)
        {
            throw new ContentValidationException(messages["PermissionWriteDenied"]);
        }
    }

    /// <summary>Benutzer verwalten dürfen nur Verwalter — die Absicherung hinter der Oberfläche.</summary>
    public async Task EnsureAdministratorAsync(CancellationToken ct = default)
    {
        if (!(await permissions.GetCurrentAsync(ct)).IsAdministrator)
        {
            throw new ContentValidationException(messages["PermissionAdminOnly"]);
        }
    }
}
