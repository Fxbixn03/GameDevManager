using System.Globalization;

namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Whiteboard des Projekts: eine freie Fläche für Skizzen und angeheftete Notizen,
/// inspiriert von Miro. Mehrere Nutzer können gleichzeitig daran arbeiten — jede gespeicherte
/// Änderung wird den anderen offenen Ansichten gemeldet.
/// <para>
/// Wie die Kanban-Boards sind Whiteboards Werkzeug-Daten: Sie beschreiben die Arbeit am
/// Spiel, nicht das Spiel — sie stehen nicht im Export und überstehen den ersetzenden Import.
/// </para>
/// </summary>
public class Whiteboard
{
    /// <summary>Feste Zeichenfläche in abstrakten Einheiten — die Ansicht skaliert sie.</summary>
    public const double CanvasWidth = 1600;

    public const double CanvasHeight = 900;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<WhiteboardNote> Notes { get; set; } = [];

    public List<WhiteboardStroke> Strokes { get; set; } = [];
}

/// <summary>Eine angeheftete Notiz — ein Klebezettel mit Text und Farbe.</summary>
public class WhiteboardNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WhiteboardId { get; set; }

    public Whiteboard? Whiteboard { get; set; }

    /// <summary>Linke obere Ecke in Zeichenflächen-Einheiten.</summary>
    public double X { get; set; }

    public double Y { get; set; }

    public string? Text { get; set; }

    /// <summary>Farbe des Zettels als Hex-Wert; ohne Angabe das Akzentgelb.</summary>
    public string? Color { get; set; }
}

/// <summary>
/// Ein Freihand-Strich. Die Punkte stehen wie bei den Polygon-Gebieten der Karten als Text
/// in fester Kultur — so gehen sie ohne eigene Tabelle durch Speichern und Duplizieren.
/// </summary>
public class WhiteboardStroke
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WhiteboardId { get; set; }

    public Whiteboard? Whiteboard { get; set; }

    /// <summary>Punktliste <c>"x,y;x,y;…"</c> in Zeichenflächen-Einheiten, feste Kultur.</summary>
    public required string Points { get; set; }

    public string? Color { get; set; }

    public double Width { get; set; } = 3;

    /// <summary>Liest die Punktliste; ein unlesbarer Eintrag macht die ganze Liste leer.</summary>
    public static List<MapPoint> ParsePoints(string? points) => MapMarker.ParsePoints(points);

    /// <summary>Schreibt die Punktliste in fester Kultur und Rundung auf ganze Zehntel.</summary>
    public static string FormatPoints(IReadOnlyCollection<MapPoint> points) =>
        string.Join(';', points.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.X:0.#},{p.Y:0.#}")));
}
