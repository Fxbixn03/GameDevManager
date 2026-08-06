namespace GameDevManager.Data.Assets;

/// <summary>
/// Legt Assets im Dateisystem ab, je Spielprojekt in einem eigenen Unterverzeichnis.
/// Der Dateiname ist die GUID des Assets — damit kollidiert nichts, und der ursprüngliche
/// Name des Nutzers muss nicht bereinigt werden, weil er nur in der Datenbank steht.
/// </summary>
public class FileSystemAssetStorage(AssetStorageOptions options) : IAssetStorage
{
    public async Task<string> SaveAsync(
        Guid projectId, Guid assetId, string extension, Stream content, CancellationToken ct = default)
    {
        var storageKey = $"{projectId:N}/{assetId:N}{extension}";
        var path = ResolvePath(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var target = File.Create(path);
        await content.CopyToAsync(target, ct);

        return storageKey;
    }

    public Stream? OpenRead(string storageKey)
    {
        var path = ResolvePath(storageKey);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string storageKey)
    {
        var path = ResolvePath(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Setzt den Schlüssel auf einen absoluten Pfad um und stellt sicher, dass er innerhalb
    /// der Wurzel bleibt. Die Schlüssel stammen zwar aus der eigenen Datenbank, aber ein
    /// Ausbruch über „..“ wäre zu folgenreich, um ihn ungeprüft zu lassen.
    /// </summary>
    private string ResolvePath(string storageKey)
    {
        var root = Path.GetFullPath(options.RootPath);
        var relative = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relative));

        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Der Asset-Schlüssel '{storageKey}' zeigt aus dem Speicher heraus.");
        }

        return full;
    }
}
