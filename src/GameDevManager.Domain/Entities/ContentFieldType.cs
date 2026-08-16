namespace GameDevManager.Domain.Entities;

/// <summary>
/// Datentyp eines benutzerdefinierten Feldes. Bestimmt, welche Spalte in
/// <see cref="FieldValue"/> den Wert trägt und welches Eingabeelement die Maske zeigt.
/// <para>
/// Die Zahlenwerte sind fest vergeben, weil sie in der Datenbank stehen. Neue Typen
/// werden hinten angehängt, bestehende nie umnummeriert.
/// </para>
/// </summary>
public enum ContentFieldType
{
    /// <summary>Einzeilige Zeichenkette.</summary>
    Text = 0,

    /// <summary>Mehrzeiliger Fließtext, z. B. Beschreibungen oder Flavour-Text.</summary>
    MultilineText = 1,

    /// <summary>Ganze Zahl, z. B. Stack-Größe oder Level-Anforderung.</summary>
    Integer = 2,

    /// <summary>Kommazahl, z. B. Gewicht oder Angriffsgeschwindigkeit.</summary>
    Decimal = 3,

    /// <summary>Ja/Nein, z. B. „handelbar".</summary>
    Boolean = 4,

    /// <summary>Datum ohne Uhrzeit.</summary>
    Date = 5,

    /// <summary>Auswahl aus den am Feld hinterlegten <see cref="FieldOption"/>en.</summary>
    Select = 6,

    /// <summary>Verweis auf eine andere Entität über deren GUID — Zielmodul steht am Feld.</summary>
    EntityReference = 7,

    /// <summary>Farbe als Hex-Wert.</summary>
    Color = 8,

    /// <summary>
    /// Verweis auf eine <see cref="Rarity"/> — eine <see cref="EntityReference"/> mit fest
    /// verdrahtetem Zielmodul, damit „Seltenheit“ direkt als Feldtyp wählbar ist.
    /// </summary>
    Rarity = 9,

    /// <summary>
    /// Levelkurve oder Formel: ein Ausdruck über <c>x</c> und/oder eine Wertetabelle, samt
    /// Vorschau-Diagramm. Der Wert steht als JSON in <see cref="FieldValue.TextValue"/> —
    /// siehe <see cref="Curves.CurveDefinition"/>.
    /// </summary>
    Curve = 10,

    /// <summary>
    /// Berechnetes Feld: ein Ausdruck über die <b>anderen Felder derselben Entität</b>
    /// (<c>Schaden * Angriffsgeschwindigkeit</c>). Die Formel steht als Text in
    /// <see cref="FieldValue.TextValue"/>; gespeichert wird nur sie, nie ihr Ergebnis — das
    /// veraltete beim ersten Umbau der Zahlen darunter.
    /// <para>
    /// Ein eigener Typ und kein Schalter an <see cref="Curve"/>: Eine Kurve hat eine Spanne,
    /// eine Wertetabelle und ein Diagramm; ein berechnetes Feld hat genau eine Zahl. Die
    /// beiden teilen sich nur den Parser.
    /// </para>
    /// </summary>
    Formula = 11
}
