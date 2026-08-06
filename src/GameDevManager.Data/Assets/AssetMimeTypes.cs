namespace GameDevManager.Data.Assets;

/// <summary>
/// Zuordnung von Dateiendung zu MIME-Typ. Nötig, weil Browser beim Hochladen nicht immer
/// einen brauchbaren Typ melden — manche schicken für Sprites „application/octet-stream“
/// oder eine leere Zeichenkette.
/// </summary>
public static class AssetMimeTypes
{
    public static string? FromFileName(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => null
        };
}
