namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein benannter Ausschnitt aus einem <see cref="Asset"/> — eine Zelle eines Sprite-Sheets,
/// ein einzelnes Symbol aus einem Atlas.
/// <para>
/// Das Tool <b>schneidet nicht</b>, es verwaltet nur die Ausschnitte: Für das Schneiden führte
/// kein Weg an einer Bildbibliothek vorbei, und die Engine wendet ein Rechteck ohnehin selbst
/// an — für Unity ist der Atlas mit Sprite-Rects sogar der gebräuchlichere Weg als 24
/// Einzeldateien. Der Export gibt die Ausschnitte als Metadaten mit, dieselbe Linie wie beim
/// <c>ImageDimensionReader</c>, der die Maße liest, ohne das Bild zu decodieren.
/// </para>
/// <para>
/// Gemessen wird in <b>Pixeln</b> und nicht relativ wie beim <see cref="MapMarker"/>. Ein
/// Raster ist in Pixeln definiert („32×32“), die Engine erwartet ein Pixel-Rechteck, und die
/// Maße des Bildes stehen am Asset — relative Werte wären an jeder Stelle zurückzurechnen und
/// brächten bei krummen Rastern Rundungsfehler mit. Die Karten-Markierung hat den umgekehrten
/// Fall: Dort wird dasselbe Bild später in höherer Auflösung ersetzt, hier ändert ein neues
/// Raster ohnehin die Ausschnitte.
/// </para>
/// </summary>
public class AssetRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    /// <summary>
    /// Name des Ausschnitts. Er wird in der Engine zum Bezeichner des Sprites und ist deshalb
    /// je Asset eindeutig — zwei „walk_01“ in einem Atlas wären nicht auseinanderzuhalten.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>Linke Kante in Pixeln, gezählt vom linken Bildrand.</summary>
    public int X { get; set; }

    /// <summary>Obere Kante in Pixeln, gezählt vom oberen Bildrand.</summary>
    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int SortOrder { get; set; }
}
