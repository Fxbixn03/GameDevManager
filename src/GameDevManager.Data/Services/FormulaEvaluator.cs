using GameDevManager.Domain.Curves;
using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>Ein berechnetes Feld samt Ergebnis — <c>null</c>, wenn die Formel nicht aufgeht.</summary>
public sealed record ComputedField(Guid FieldDefinitionId, string Name, string Formula, double? Value);

/// <summary>
/// Rechnet Formeln aus, die auf andere Felder derselben Entität verweisen —
/// <c>Schaden * Angriffsgeschwindigkeit</c>.
/// <para>
/// Ein berechnetes Feld ist ein <see cref="ContentFieldType.Formula"/>; seine Formel steht als
/// Text in <see cref="FieldValue.TextValue"/>. <b>Gespeichert wird nur die Formel, nie ihr
/// Ergebnis</b> — sonst veraltete es beim ersten Umbau der Zahlen, auf die es sich stützt.
/// Gerechnet wird bei jeder Anzeige und einmal beim Export.
/// </para>
/// <para>
/// Verwiesen wird über den <b>Feldnamen</b>, nicht über die GUID: Wer eine Formel schreibt,
/// hat den Namen vor Augen, und der Ausdruck bliebe mit GUIDs unlesbar. Verglichen wird in
/// einer Normalform (kleingeschrieben, ohne Leerzeichen), damit „Max. Leben“ als
/// <c>maxleben</c> ansprechbar ist — dieselbe Überlegung wie beim Bezeichner der
/// Engine-Presets.
/// </para>
/// </summary>
public static class FormulaEvaluator
{
    /// <summary>
    /// Rechnet alle berechneten Felder einer Entität aus. Ein Feld, dessen Formel nicht
    /// aufgeht oder das in einem Ring steht, bekommt <c>null</c> statt einer erfundenen Zahl.
    /// </summary>
    public static List<ComputedField> Compute(
        IEnumerable<FieldDefinition> fields, IReadOnlyDictionary<Guid, FieldValue> values)
    {
        var applicable = fields.ToList();

        // Die Zahlen, auf die sich eine Formel stützen kann: alles, was einen Zahlenwert trägt.
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var field in applicable.Where(field => field.Type is ContentFieldType.Integer or ContentFieldType.Decimal))
        {
            if (values.TryGetValue(field.Id, out var value) && value.NumberValue is { } number)
            {
                numbers[Normalize(field.Name)] = number;
            }
        }

        var formulas = applicable
            .Where(field => field.Type == ContentFieldType.Formula)
            .ToDictionary(field => Normalize(field.Name), field => field, StringComparer.Ordinal);

        var resolved = new Dictionary<string, double?>(StringComparer.Ordinal);
        var results = new List<ComputedField>();

        foreach (var field in applicable.Where(field => field.Type == ContentFieldType.Formula))
        {
            var text = values.TryGetValue(field.Id, out var value) ? value.TextValue : null;

            results.Add(new ComputedField(
                field.Id,
                field.Name,
                text ?? string.Empty,
                Resolve(Normalize(field.Name), text, numbers, formulas, values, resolved, [])));
        }

        return results;
    }

    /// <summary>
    /// Der Wert einer Formel. Verweise auf andere berechnete Felder werden dabei aufgelöst —
    /// <paramref name="path"/> bricht Ringe ab, wie in der Zyklenprüfung der Rezepte.
    /// </summary>
    private static double? Resolve(
        string key,
        string? formula,
        Dictionary<string, double> numbers,
        Dictionary<string, FieldDefinition> formulas,
        IReadOnlyDictionary<Guid, FieldValue> values,
        Dictionary<string, double?> resolved,
        HashSet<string> path)
    {
        if (resolved.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!path.Add(key) || !CurveExpression.TryParse(formula, out var expression))
        {
            // Ein Ring oder eine kaputte Formel: kein Wert, keine Ausnahme. Wer sie ausbaden
            // müsste, käme sonst an seinem eigenen Datensatz nicht mehr vorbei.
            return null;
        }

        var lookup = new Dictionary<string, double>(numbers, StringComparer.Ordinal);

        foreach (var reference in expression!.References)
        {
            if (lookup.ContainsKey(reference))
            {
                continue;
            }

            if (!formulas.TryGetValue(reference, out var other))
            {
                continue;
            }

            var text = values.TryGetValue(other.Id, out var value) ? value.TextValue : null;
            var nested = Resolve(reference, text, numbers, formulas, values, resolved, path);

            if (nested is { } number)
            {
                lookup[reference] = number;
            }
        }

        path.Remove(key);

        // Die Variable x hat in einer Feldformel keine Bedeutung — sie gehört zur Levelkurve.
        var result = expression.Evaluate(0, lookup);
        var computed = double.IsFinite(result) ? result : (double?)null;

        if (path.Count == 0)
        {
            resolved[key] = computed;
        }

        return computed;
    }

    /// <summary>
    /// Vergleichsform eines Feldnamens: kleingeschrieben, ohne alles, was kein Buchstabe und
    /// keine Ziffer ist. „Max. Leben“ ist damit als <c>maxleben</c> ansprechbar — genau die
    /// Form, die auch der Tokenizer aus einem Bezeichner macht.
    /// </summary>
    public static string Normalize(string name) =>
        new([.. name.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
}
