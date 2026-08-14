using System.Text.Json;
using System.Text.Json.Nodes;
using GameDevManager.Data;
using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Persists settings changed in the UI to <c>appsettings.Local.json</c> in the content root.
/// The file is loaded on top of <c>appsettings.json</c> at startup (see <c>Program.cs</c>), so
/// changes apply after a restart and the checked-in <c>appsettings.json</c> stays untouched.
/// </summary>
public class LocalSettingsFile(IHostEnvironment environment)
{
    public const string FileName = "appsettings.Local.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private string FilePath => Path.Combine(environment.ContentRootPath, FileName);

    /// <summary>
    /// Stores provider and connection string. Existing content of the file is preserved —
    /// only <c>Database:Provider</c> and the chosen provider's connection string are replaced,
    /// so previously saved strings of other providers survive a switch back.
    /// </summary>
    public async Task WriteDatabaseSettingsAsync(
        DatabaseProvider provider, string connectionString, CancellationToken ct = default)
    {
        var root = await ReadAsync(ct);

        var database = root["Database"] as JsonObject ?? new JsonObject();
        database["Provider"] = provider.ToString();
        root["Database"] = database;

        var connectionStrings = root["ConnectionStrings"] as JsonObject ?? new JsonObject();
        connectionStrings[provider.ToString()] = connectionString;
        root["ConnectionStrings"] = connectionStrings;

        await File.WriteAllTextAsync(FilePath, root.ToJsonString(WriteOptions), ct);
    }

    /// <summary>
    /// Merkt sich das zuletzt gewählte Spielprojekt, damit die Auswahl einen Neustart
    /// überlebt. Beim Start liest die <see cref="ProjectSelection"/> den Wert aus der
    /// Konfiguration (<c>Project:CurrentId</c>).
    /// </summary>
    public async Task WriteCurrentProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var root = await ReadAsync(ct);

        var project = root["Project"] as JsonObject ?? new JsonObject();
        project["CurrentId"] = projectId.ToString();
        root["Project"] = project;

        await File.WriteAllTextAsync(FilePath, root.ToJsonString(WriteOptions), ct);
    }

    /// <summary>
    /// Merkt sich die Hell/Dunkel-Wahl. Wie die Projektauswahl gilt sie installationsweit
    /// und nicht je Browser: Das Tool wird self-hosted von einer Person betrieben, und ein
    /// Wert im Browserspeicher wäre beim nächsten Gerät wieder weg.
    /// </summary>
    public async Task WriteDarkModeAsync(bool isDarkMode, CancellationToken ct = default)
    {
        var root = await ReadAsync(ct);

        var appearance = root["Appearance"] as JsonObject ?? new JsonObject();
        appearance["DarkMode"] = isDarkMode;
        root["Appearance"] = appearance;

        await File.WriteAllTextAsync(FilePath, root.ToJsonString(WriteOptions), ct);
    }

    /// <summary>
    /// Merkt sich die Passwortrichtlinie. Installationsweit wie die übrigen Werte hier — und
    /// bewusst keine Datenbanktabelle, die eine Migration in allen vier Providern verlangte.
    /// Beim Start liest die <see cref="PasswordPolicySelection"/> die Werte aus der
    /// Konfiguration (<c>PasswordPolicy:*</c>).
    /// </summary>
    public async Task WritePasswordPolicyAsync(PasswordPolicy policy, CancellationToken ct = default)
    {
        var root = await ReadAsync(ct);

        var section = root["PasswordPolicy"] as JsonObject ?? new JsonObject();
        section["MinimumLength"] = policy.MinimumLength;
        section["RequireDigit"] = policy.RequireDigit;
        section["RequireSpecialCharacter"] = policy.RequireSpecialCharacter;
        section["PasswordsDisabled"] = policy.PasswordsDisabled;
        root["PasswordPolicy"] = section;

        await File.WriteAllTextAsync(FilePath, root.ToJsonString(WriteOptions), ct);
    }

    private async Task<JsonObject> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(FilePath);
        return await JsonNode.ParseAsync(stream, cancellationToken: ct) as JsonObject ?? [];
    }
}
