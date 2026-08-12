using System.Text.Json;
using System.Text.Json.Nodes;
using GameDevManager.Data;

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
