using System.Globalization;

namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Markierung auf einer Karte: ein Punkt, ein Kreis-Bereich oder ein Polygon-Gebiet.
/// <para>
/// Position und Radius sind <b>relativ</b> zur Bildgröße (0 bis 1) und nicht in Pixeln. Damit
/// bleiben die Markierungen richtig, egal wie groß die Karte gerade dargestellt wird — und
/// auch dann, wenn dasselbe Bild später in höherer Auflösung neu hochgeladen wird.
/// </para>
/// <para>
/// Worauf die Markierung zeigt, steht als Modul-Schlüssel und GUID daran. Damit deckt ein
/// einziges Modell alle Fälle des Konzepts ab: der Spawn-Ort eines NPCs, die Verknüpfung auf
/// eine andere Karte und später das Gebiet einer Fraktion.
/// </para>
/// </summary>
public class MapMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MapId { get; set; }

    public GameMap? Map { get; set; }

    /// <summary>Waagerechte Lage, 0 = linker Rand, 1 = rechter Rand.</summary>
    public double X { get; set; }

    /// <summary>Senkrechte Lage, 0 = oberer Rand, 1 = unterer Rand.</summary>
    public double Y { get; set; }

    /// <summary>
    /// Radius relativ zur Bildbreite. <c>null</c> heißt Punkt; ein Wert macht daraus einen
    /// Bereich — etwa das Gebiet, in dem eine Mob-Art vorkommt.
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Eckpunkte eines Polygon-Gebiets — das „Gebiete der Fraktionen einzeichnen“ aus dem
    /// Konzept, für das der Kreis nur eine Näherung war. Relativ zur Bildgröße wie
    /// <see cref="X"/> und <see cref="Y"/>, als Text <c>"x,y;x,y;…"</c> in fester Kultur:
    /// So geht die Liste ohne eigene Tabelle durch Export, Import und Duplizieren.
    /// <c>null</c> heißt Punkt oder Kreis-Bereich; ein Polygon braucht mindestens drei Punkte
    /// und schließt den <see cref="Radius"/> aus. X und Y bleiben der Anker der Beschriftung.
    /// </summary>
    public string? Points { get; set; }

    public string? Label { get; set; }

    /// <summary>Modul der Zielentität — siehe <see cref="ModuleKeys"/>. <c>null</c> bei reinen Notizen.</summary>
    public string? TargetModuleKey { get; set; }

    /// <summary>GUID der Zielentität, ohne Fremdschlüssel wie alle modulübergreifenden Verweise.</summary>
    public Guid? TargetEntityId { get; set; }

    /// <summary>
    /// Werkzeug-Asset als Symbol. Genau dafür sieht das Konzept Assets ohne Entität vor:
    /// „Marker für die Karten/Maps“.
    /// </summary>
    public Guid? IconAssetId { get; set; }

    /// <summary>Farbe als Hex-Wert; ohne Angabe wird das Akzentgelb verwendet.</summary>
    public string? Color { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Ein Kreis-Bereich statt eines Punktes.</summary>
    public bool IsArea => Radius is > 0;

    /// <summary>Ein Polygon-Gebiet — siehe <see cref="Points"/>.</summary>
    public bool IsPolygon => !string.IsNullOrWhiteSpace(Points);

    /// <summary>Zeigt auf eine andere Karte — das ist die Verknüpfung aus dem Konzept.</summary>
    public bool IsMapLink => TargetModuleKey == ModuleKeys.Maps && TargetEntityId is not null;

    /// <summary>Die Eckpunkte aus <see cref="Points"/>; leer, wenn keine gesetzt sind.</summary>
    public List<MapPoint> GetPolygonPoints() => ParsePoints(Points);

    /// <summary>
    /// Liest die Punktliste. Ein unlesbarer Eintrag macht die ganze Liste leer — die
    /// Validierung behandelt das wie „zu wenige Punkte“, statt ein halbes Polygon zu zeigen.
    /// </summary>
    public static List<MapPoint> ParsePoints(string? points)
    {
        if (string.IsNullOrWhiteSpace(points))
        {
            return [];
        }

        var result = new List<MapPoint>();

        foreach (var pair in points.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(',');

            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return [];
            }

            result.Add(new MapPoint(x, y));
        }

        return result;
    }

    /// <summary>
    /// Schreibt die Punktliste in fester Kultur und fester Rundung — derselbe Stand ergibt
    /// so denselben Export, die Grundlage der Diff-Ansicht.
    /// </summary>
    public static string? FormatPoints(IReadOnlyCollection<MapPoint> points) =>
        points.Count == 0
            ? null
            : string.Join(';', points.Select(p =>
                string.Create(CultureInfo.InvariantCulture, $"{p.X:0.####},{p.Y:0.####}")));
}

/// <summary>Ein Eckpunkt eines Polygon-Gebiets, relativ zur Bildgröße (0 bis 1).</summary>
public readonly record struct MapPoint(double X, double Y);
