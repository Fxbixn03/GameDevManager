using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDevManager.Domain.Curves;

/// <summary>Ein Punkt der Wertetabelle: bei <c>x</c> gilt <c>y</c>.</summary>
public sealed class CurvePoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>
/// Eine Levelkurve oder Schadensformel: entweder ein Ausdruck über <c>x</c>, den das Tool über
/// eine Spanne auswertet, oder eine von Hand gepflegte Wertetabelle.
/// <para>
/// Beides in einem Feldtyp, weil beides dieselbe Frage beantwortet — „welcher Wert gilt bei
/// welcher Stufe“ — und weil man beim Balancing zwischen beidem wechselt: erst eine Formel als
/// Grundlage, dann einzelne Stufen von Hand nachziehen. <see cref="Overrides"/> hält genau das
/// fest, ohne die Formel zu verlieren.
/// </para>
/// <para>
/// Gespeichert wird der ganze Aufbau als JSON in <c>FieldValue.TextValue</c>. Eine eigene
/// Tabelle bekäme er nicht: Feldwerte hängen modulübergreifend an einer GUID, und eine Kurve
/// ist ein Wert wie jeder andere — so geht sie ohne Zutun durch Export, Import, Duplizieren
/// und die Feldvererbung der Unterarten.
/// </para>
/// </summary>
public sealed class CurveDefinition
{
    /// <summary>Formel über <c>x</c>, z. B. <c>100 * x ^ 1.5</c>. Leer heißt reine Wertetabelle.</summary>
    public string? Expression { get; set; }

    /// <summary>Erster Wert von <c>x</c> — üblicherweise Stufe 1.</summary>
    public double From { get; set; } = 1;

    /// <summary>Letzter Wert von <c>x</c>.</summary>
    public double To { get; set; } = 10;

    /// <summary>Schrittweite. Muss größer als null sein, sonst gilt <see cref="DefaultStep"/>.</summary>
    public double Step { get; set; } = DefaultStep;

    /// <summary>
    /// Von Hand gesetzte Werte. Ohne Formel sind sie die ganze Kurve; mit Formel überschreiben
    /// sie einzelne Stufen — der „Boss auf Stufe 50 kriegt einen Sprung“-Fall.
    /// </summary>
    public List<CurvePoint> Overrides { get; set; } = [];

    /// <summary>Einheit der y-Werte zur reinen Anzeige, z. B. „HP“ oder „Schaden“.</summary>
    public string? Unit { get; set; }

    public const double DefaultStep = 1;

    /// <summary>Mehr Punkte zeichnet die Vorschau nicht — darunter wird die Kurve zur Fläche.</summary>
    public const int MaxPoints = 500;

    [JsonIgnore]
    public bool HasExpression => !string.IsNullOrWhiteSpace(Expression);

    [JsonIgnore]
    public bool IsEmpty => !HasExpression && Overrides.Count == 0;

    /// <summary>
    /// Die ausgerechnete Wertetabelle. Ohne Formel sind es die von Hand gesetzten Punkte;
    /// mit Formel die Spanne, in der ein gesetzter Wert den gerechneten ersetzt.
    /// <para>
    /// Punkte, an denen die Formel nicht rechnet (Wurzel aus einer negativen Zahl, Division
    /// durch null), fallen heraus statt die ganze Kurve zu verwerfen — an den übrigen Stufen
    /// stimmt sie ja.
    /// </para>
    /// </summary>
    public IReadOnlyList<CurvePoint> Sample()
    {
        var overrides = Overrides
            .GroupBy(point => point.X)
            .ToDictionary(group => group.Key, group => group.Last().Y);

        if (!HasExpression)
        {
            return [.. overrides.OrderBy(pair => pair.Key).Select(pair => new CurvePoint { X = pair.Key, Y = pair.Value })];
        }

        if (!CurveExpression.TryParse(Expression, out var expression) || expression is null)
        {
            return [];
        }

        var step = Step > 0 ? Step : DefaultStep;
        var points = new List<CurvePoint>();

        // Über den Zähler statt über eine aufaddierte Laufvariable: Bei einer Schrittweite wie
        // 0,1 liefe die Summe langsam weg, und die Stützstellen träfen die von Hand gesetzten
        // Werte nicht mehr. Zugleich ist die Zahl der Punkte damit vorher bekannt.
        var count = (int)Math.Floor((To - From) / step) + 1;

        for (var index = 0; index < count && points.Count < MaxPoints; index++)
        {
            var x = From + index * step;
            var y = overrides.TryGetValue(x, out var manual) ? manual : expression.Evaluate(x);

            if (double.IsFinite(y))
            {
                points.Add(new CurvePoint { X = x, Y = y });
            }
        }

        // Von Hand gesetzte Werte außerhalb der Spanne gehen nicht verloren — wer Stufe 99
        // einträgt, während die Formel bis 60 läuft, meint sie auch.
        foreach (var (x, y) in overrides.Where(pair => pair.Key < From || pair.Key > To))
        {
            points.Add(new CurvePoint { X = x, Y = y });
        }

        return [.. points.OrderBy(point => point.X)];
    }

    /// <summary>Der Wert an einer Stelle, oder <c>null</c>, wenn die Kurve dort nichts hergibt.</summary>
    public double? ValueAt(double x)
    {
        var manual = Overrides.LastOrDefault(point => point.X == x);
        if (manual is not null)
        {
            return manual.Y;
        }

        if (!CurveExpression.TryParse(Expression, out var expression) || expression is null)
        {
            return null;
        }

        var y = expression.Evaluate(x);
        return double.IsFinite(y) ? y : null;
    }

    // ---------------------------------------------------------------------- Speicherformat

    private static readonly JsonSerializerOptions StorageOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Liest eine Kurve aus dem gespeicherten Text. Alles, was kein Kurven-JSON ist, ergibt
    /// <c>null</c> — ein Feld, das erst später auf „Formel/Kurve“ umgestellt wurde, trägt dann
    /// noch seinen alten Text und darf davon nicht umfallen.
    /// </summary>
    public static CurveDefinition? Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CurveDefinition>(stored, StorageOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Schreibt die Kurve in ihre Textform. Eine leere Kurve ergibt <c>null</c> — kein leerer Wert in der Datenbank.</summary>
    public string? Serialize() =>
        IsEmpty ? null : JsonSerializer.Serialize(this, StorageOptions);

    /// <summary>Kurzfassung für Listen: die Formel, sonst die Zahl der Stützstellen.</summary>
    public string Describe(string tableFormat) =>
        HasExpression
            ? Expression!.Trim()
            : string.Format(CultureInfo.CurrentCulture, tableFormat, Overrides.Count);
}
