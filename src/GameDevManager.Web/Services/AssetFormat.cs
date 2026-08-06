using GameDevManager.Domain.Entities;

namespace GameDevManager.Web.Services;

/// <summary>Anzeigetexte rund um Assets — Dateigröße, Maße, Kurzbeschreibung.</summary>
public static class AssetFormat
{
    public static string Size(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.#} MB",
        >= 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes} B"
    };

    /// <summary>Maße in Pixeln, oder ein Gedankenstrich für Formate ohne lesbaren Kopf (SVG).</summary>
    public static string Dimensions(Asset asset) =>
        asset is { Width: { } width, Height: { } height } ? $"{width} × {height}" : "—";

    /// <summary>Einzeiler für Kacheln: Maße und Größe.</summary>
    public static string Summary(Asset asset) =>
        $"{Dimensions(asset)} · {Size(asset.SizeBytes)}";
}
