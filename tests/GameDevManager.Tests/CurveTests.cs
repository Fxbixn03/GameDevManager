using Xunit;
using GameDevManager.Domain.Curves;

namespace GameDevManager.Tests;

/// <summary>
/// Der Feldtyp „Formel/Kurve“: der Ausdrucksrechner und die Wertetabelle daraus.
/// <para>
/// Reine Rechenlogik ohne Datenbank — deshalb ohne <see cref="TestDatabase"/>. Geprüft wird
/// vor allem die Rechenreihenfolge: Sie entscheidet, ob eine Levelkurve das tut, was der
/// Nutzer hingeschrieben hat, und ein falsches Ergebnis fiele erst im Spiel auf.
/// </para>
/// </summary>
public class CurveTests
{
    [Theory]
    [InlineData("1 + 2 * 3", 0, 7)]                 // Punkt vor Strich
    [InlineData("(1 + 2) * 3", 0, 9)]               // Klammern schlagen das
    [InlineData("2 ^ 3 ^ 2", 0, 512)]               // Potenz ist rechtsassoziativ: 2^(3^2)
    [InlineData("-x ^ 2", 3, -9)]                   // Vorzeichen bindet schwächer als die Potenz
    [InlineData("(-x) ^ 2", 3, 9)]
    [InlineData("2 ^ -2", 0, 0.25)]                 // Vorzeichen im Exponenten
    [InlineData("100 * x", 5, 500)]
    [InlineData("10 - 4 - 3", 0, 3)]                // links nach rechts
    [InlineData("max(3, x)", 7, 7)]
    [InlineData("min(3, x)", 7, 3)]
    [InlineData("floor(x / 2)", 7, 3)]
    [InlineData("round(x / 2)", 7, 4)]
    [InlineData("sqrt(x)", 16, 4)]
    [InlineData("pow(x, 3)", 2, 8)]
    [InlineData("abs(0 - x)", 5, 5)]
    [InlineData("10 % 3", 0, 1)]
    public void Der_Rechner_wertet_Ausdruecke_in_der_richtigen_Reihenfolge_aus(
        string expression, double x, double expected)
    {
        var parsed = CurveExpression.Parse(expression);

        Assert.Equal(expected, parsed.Evaluate(x), 6);
    }

    [Fact]
    public void Zahlen_werden_unabhaengig_von_der_Kultur_gelesen()
    {
        // Derselbe Ausdruck muss auf jedem Rechner dieselbe Kurve ergeben — er geht so auch
        // in den Export.
        Assert.Equal(2.5, CurveExpression.Parse("x * 0.5").Evaluate(5), 6);
    }

    [Theory]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("wurzel(4)")]
    [InlineData("x $ 2")]
    [InlineData("")]
    public void Ein_kaputter_Ausdruck_wird_abgelehnt_statt_falsch_gerechnet(string expression)
    {
        Assert.False(CurveExpression.TryParse(expression, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void Die_Wertetabelle_folgt_der_Spanne_und_der_Schrittweite()
    {
        var curve = new CurveDefinition { Expression = "100 * x", From = 1, To = 5, Step = 1 };

        var points = curve.Sample();

        Assert.Equal(5, points.Count);
        Assert.Equal(100, points[0].Y);
        Assert.Equal(500, points[^1].Y);
    }

    [Fact]
    public void Von_Hand_gesetzte_Werte_ersetzen_an_ihrer_Stelle_die_Formel()
    {
        // Der „Boss auf Stufe 3 kriegt einen Sprung“-Fall: Die Formel bleibt stehen, eine
        // einzelne Stufe wird nachgezogen.
        var curve = new CurveDefinition
        {
            Expression = "10 * x",
            From = 1,
            To = 4,
            Step = 1,
            Overrides = [new CurvePoint { X = 3, Y = 999 }]
        };

        var points = curve.Sample();

        Assert.Equal([10, 20, 999, 40], points.Select(point => point.Y).ToArray());
    }

    [Fact]
    public void Werte_ausserhalb_der_Spanne_gehen_nicht_verloren()
    {
        var curve = new CurveDefinition
        {
            Expression = "x",
            From = 1,
            To = 2,
            Step = 1,
            Overrides = [new CurvePoint { X = 99, Y = 5000 }]
        };

        var points = curve.Sample();

        Assert.Equal(3, points.Count);
        Assert.Equal(99, points[^1].X);
    }

    [Fact]
    public void Ohne_Formel_ist_die_Wertetabelle_die_ganze_Kurve()
    {
        var curve = new CurveDefinition
        {
            Overrides =
            [
                new CurvePoint { X = 3, Y = 30 },
                new CurvePoint { X = 1, Y = 10 }
            ]
        };

        var points = curve.Sample();

        Assert.Equal([1d, 3d], points.Select(point => point.X).ToArray());
        Assert.False(curve.HasExpression);
    }

    [Fact]
    public void Stellen_an_denen_die_Formel_nicht_rechnet_fallen_heraus()
    {
        // Wurzel aus einer negativen Zahl ergibt NaN — die übrigen Stufen bleiben gültig,
        // die ganze Kurve deswegen zu verwerfen wäre die schlechtere Antwort.
        var curve = new CurveDefinition { Expression = "sqrt(x)", From = -2, To = 2, Step = 1 };

        var points = curve.Sample();

        Assert.Equal([0d, 1d, 2d], points.Select(point => point.X).ToArray());
    }

    [Fact]
    public void Eine_Kurve_ueberlebt_das_Speichern_und_Wiederlesen()
    {
        var curve = new CurveDefinition
        {
            Expression = "50 * x ^ 1.5",
            From = 1,
            To = 60,
            Step = 1,
            Unit = "HP",
            Overrides = [new CurvePoint { X = 50, Y = 12345 }]
        };

        var restored = CurveDefinition.Parse(curve.Serialize());

        Assert.NotNull(restored);
        Assert.Equal(curve.Expression, restored.Expression);
        Assert.Equal(curve.Unit, restored.Unit);
        Assert.Equal(60, restored.To);
        Assert.Equal(12345, Assert.Single(restored.Overrides).Y);
    }

    [Fact]
    public void Ein_Textwert_der_keine_Kurve_ist_faellt_nicht_um()
    {
        // Ein Feld, das erst später auf „Formel/Kurve“ umgestellt wurde, trägt noch seinen
        // alten Text — der darf die Maske nicht zerlegen.
        Assert.Null(CurveDefinition.Parse("Nahkampfschaden, ungefähr doppelt so hoch"));
        Assert.Null(CurveDefinition.Parse(null));
    }

    [Fact]
    public void Eine_leere_Kurve_hinterlaesst_keinen_Wert()
    {
        Assert.Null(new CurveDefinition().Serialize());
    }
}
