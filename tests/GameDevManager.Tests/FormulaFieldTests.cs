using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Curves;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Berechnete Felder: eine Formel über die anderen Felder derselben Entität. Gespeichert wird
/// nur die Formel — geprüft wird deshalb vor allem das Rechnen, samt Ringen und kaputten
/// Ausdrücken.
/// </summary>
public class FormulaFieldTests
{
    private static FieldDefinition Number(string name) => new()
    {
        ModuleKey = ModuleKeys.Items,
        Name = name,
        Type = ContentFieldType.Decimal
    };

    private static FieldDefinition Formula(string name) => new()
    {
        ModuleKey = ModuleKeys.Items,
        Name = name,
        Type = ContentFieldType.Formula
    };

    private static FieldValue ValueOf(FieldDefinition field, double? number = null, string? text = null) => new()
    {
        FieldDefinitionId = field.Id,
        OwnerEntityId = Guid.NewGuid(),
        OwnerModuleKey = ModuleKeys.Items,
        NumberValue = number,
        TextValue = text
    };

    [Fact]
    public void Eine_Formel_rechnet_ueber_andere_Felder()
    {
        var damage = Number("Schaden");
        var speed = Number("Angriffsgeschwindigkeit");
        var dps = Formula("DPS");

        var values = new Dictionary<Guid, FieldValue>
        {
            [damage.Id] = ValueOf(damage, 12),
            [speed.Id] = ValueOf(speed, 1.5),
            [dps.Id] = ValueOf(dps, text: "schaden * angriffsgeschwindigkeit")
        };

        var computed = Assert.Single(FormulaEvaluator.Compute([damage, speed, dps], values));

        Assert.Equal(18, computed.Value);
    }

    [Fact]
    public void Gross_Kleinschreibung_und_Leerzeichen_sind_egal()
    {
        var health = Number("Max. Leben");
        var doubled = Formula("Doppelt");

        var values = new Dictionary<Guid, FieldValue>
        {
            [health.Id] = ValueOf(health, 50),
            [doubled.Id] = ValueOf(doubled, text: "MaxLeben * 2")
        };

        // „Max. Leben“ ist als „maxleben“ ansprechbar — der Ausdruck bliebe mit GUIDs unlesbar.
        Assert.Equal(100, Assert.Single(FormulaEvaluator.Compute([health, doubled], values)).Value);
    }

    [Fact]
    public void Eine_Formel_darf_auf_eine_andere_Formel_zeigen()
    {
        var damage = Number("Schaden");
        var doubled = Formula("Doppelt");
        var quadrupled = Formula("Vierfach");

        var values = new Dictionary<Guid, FieldValue>
        {
            [damage.Id] = ValueOf(damage, 3),
            [doubled.Id] = ValueOf(doubled, text: "schaden * 2"),
            [quadrupled.Id] = ValueOf(quadrupled, text: "doppelt * 2")
        };

        var computed = FormulaEvaluator.Compute([damage, doubled, quadrupled], values);

        Assert.Equal(6, computed.Single(entry => entry.Name == "Doppelt").Value);
        Assert.Equal(12, computed.Single(entry => entry.Name == "Vierfach").Value);
    }

    [Fact]
    public void Ein_Ring_ergibt_keinen_Wert_und_keine_Ausnahme()
    {
        var first = Formula("Eins");
        var second = Formula("Zwei");

        var values = new Dictionary<Guid, FieldValue>
        {
            [first.Id] = ValueOf(first, text: "zwei + 1"),
            [second.Id] = ValueOf(second, text: "eins + 1")
        };

        // Dieselbe Tiefensuche wie bei den Rezepten — sie bricht ab, statt endlos zu laufen.
        Assert.All(FormulaEvaluator.Compute([first, second], values), entry => Assert.Null(entry.Value));
    }

    [Fact]
    public void Eine_kaputte_Formel_und_ein_unbekannter_Name_ergeben_nichts()
    {
        var broken = Formula("Kaputt");
        var unknown = Formula("Unbekannt");

        var values = new Dictionary<Guid, FieldValue>
        {
            [broken.Id] = ValueOf(broken, text: "1 +"),
            [unknown.Id] = ValueOf(unknown, text: "gibtesnicht * 2")
        };

        // Wer den Fehler ausbaden müsste, käme sonst an seinem eigenen Datensatz nicht vorbei.
        Assert.All(FormulaEvaluator.Compute([broken, unknown], values), entry => Assert.Null(entry.Value));
    }

    [Fact]
    public void Der_Parser_meldet_die_benutzten_Namen()
    {
        Assert.True(CurveExpression.TryParse("schaden * tempo + 1", out var expression));

        // Ohne x — das gehört zur Levelkurve und ist keine Abhängigkeit vom Feld.
        Assert.Equal(new[] { "schaden", "tempo" }, expression!.References.Order());

        Assert.True(CurveExpression.TryParse("100 * x ^ 1.5", out var curve));
        Assert.Empty(curve!.References);
    }

    [Fact]
    public void Die_Kurve_rechnet_weiterhin_ueber_x()
    {
        // Der Parser kennt jetzt benannte Variablen — die Levelkurve darf davon nichts merken.
        Assert.True(CurveExpression.TryParse("100 * x ^ 2", out var expression));

        Assert.Equal(400, expression!.Evaluate(2));
    }
}
