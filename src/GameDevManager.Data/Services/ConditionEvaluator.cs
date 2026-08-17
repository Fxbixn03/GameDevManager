using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Ein angenommener Spielzustand: Stufe, Beutel, Schalter, Weltzustände. Die Vorlage, gegen
/// die <see cref="ConditionEvaluator"/> rechnet — zusammengeklickt in der Oberfläche, nicht
/// gespeichert.
/// </summary>
public sealed record GameStateAssumption
{
    public int PlayerLevel { get; init; } = 1;

    /// <summary>Item-GUID → Menge im Beutel. Was fehlt, ist 0.</summary>
    public IReadOnlyDictionary<Guid, double> Items { get; init; } = new Dictionary<Guid, double>();

    /// <summary>Währungs-GUID → Betrag. Was fehlt, ist 0.</summary>
    public IReadOnlyDictionary<Guid, double> Currencies { get; init; } = new Dictionary<Guid, double>();

    /// <summary>Gesetzte Story-Schalter, verglichen ohne Groß-/Kleinschreibung.</summary>
    public IReadOnlySet<string> Flags { get; init; } = new HashSet<string>();

    /// <summary>Die gerade geltenden Weltzustände (Tageszeit, Wetter, Biom) als GUIDs.</summary>
    public IReadOnlySet<Guid> ActiveWorldStates { get; init; } = new HashSet<Guid>();

    public IReadOnlySet<Guid> DefeatedNpcs { get; init; } = new HashSet<Guid>();

    public IReadOnlySet<Guid> Unlocked { get; init; } = new HashSet<Guid>();

    /// <summary>Quest-GUID → angenommener Zustand als Text, verglichen ohne Groß-/Kleinschreibung.</summary>
    public IReadOnlyDictionary<Guid, string> QuestStates { get; init; } = new Dictionary<Guid, string>();
}

/// <summary>Das Ergebnis einer einzelnen Bedingung.</summary>
/// <param name="IsAssumption">
/// Die Bedingung ließ sich nicht mechanisch prüfen (<see cref="ConditionKind.Custom"/> oder ein
/// fehlendes Ziel) und gilt als erfüllt — ausgewiesen statt verschwiegen, damit die Anzeige
/// sagen kann: „hier nimmt das Tool etwas an“.
/// </param>
public sealed record ConditionResult(Condition Condition, bool Satisfied, bool IsAssumption);

/// <summary>Das Ergebnis eines ganzen Satzes.</summary>
public sealed record ConditionSetResult(bool Satisfied, IReadOnlyList<ConditionResult> Conditions)
{
    /// <summary>Ein Satz, den es nicht gibt, verbietet nichts.</summary>
    public static ConditionSetResult Empty { get; } = new(true, []);

    /// <summary>Die Bedingungen, an denen es scheitert — für die Anzeige „warum nicht?“.</summary>
    public IEnumerable<ConditionResult> Failed => Conditions.Where(entry => !entry.Satisfied);
}

/// <summary>
/// Wertet Bedingungssätze gegen einen angenommenen Spielzustand aus — der erste Ort, an dem das
/// Bedingungssystem <b>gerechnet</b> statt nur verwaltet wird (F11; die Zustands-Sicht aus F19
/// lebt vom selben Kern).
/// <para>
/// Bewusst eine reine Rechenklasse ohne Datenbank: Was es im Projekt gibt, weiß der Aufrufer —
/// hier steht nur, was eine Bedingung gegen einen Zustand bedeutet. Nicht Prüfbares
/// (<see cref="ConditionKind.Custom"/>, fehlende Ziele) gilt als erfüllt und wird als Annahme
/// ausgewiesen — dieselbe Zurückhaltung wie beim Health Check „unerfüllbare Bedingungen“:
/// lieber eine sichtbare Annahme als ein stummer Abbruch aus dem falschen Grund.
/// </para>
/// </summary>
public static class ConditionEvaluator
{
    public static ConditionSetResult Evaluate(ConditionSet? set, GameStateAssumption state)
    {
        if (set is null || set.Conditions.Count == 0)
        {
            return ConditionSetResult.Empty;
        }

        var results = set.Conditions
            .OrderBy(condition => condition.SortOrder)
            .Select(condition => Evaluate(condition, state))
            .ToList();

        var satisfied = set.Logic == ConditionLogic.All
            ? results.All(entry => entry.Satisfied)
            : results.Any(entry => entry.Satisfied);

        return new ConditionSetResult(satisfied, results);
    }

    public static ConditionResult Evaluate(Condition condition, GameStateAssumption state)
    {
        // Ohne Ziel ist eine zielbezogene Bedingung nicht zu rechnen — eine Annahme, kein Nein.
        if (condition.UsesTarget && condition.TargetEntityId is null)
        {
            return new ConditionResult(condition, Satisfied: true, IsAssumption: true);
        }

        var target = condition.TargetEntityId.GetValueOrDefault();

        return condition.Kind switch
        {
            ConditionKind.HasItem => Number(
                condition, state.Items.GetValueOrDefault(target)),

            ConditionKind.HasCurrency => Number(
                condition, state.Currencies.GetValueOrDefault(target)),

            ConditionKind.PlayerLevel => Number(condition, state.PlayerLevel),

            ConditionKind.Flag => Boolean(
                condition,
                condition.TextValue is { } flag && state.Flags.Contains(flag.Trim())),

            ConditionKind.NpcDefeated => Boolean(condition, state.DefeatedNpcs.Contains(target)),

            ConditionKind.Unlocked => Boolean(condition, state.Unlocked.Contains(target)),

            ConditionKind.TimeOfDay or ConditionKind.Weather or ConditionKind.Biome =>
                Boolean(condition, state.ActiveWorldStates.Contains(target)),

            ConditionKind.QuestState => QuestState(condition, state),

            // Frei Beschriebenes kennt nur der Mensch — es gilt als erfüllt und wird als
            // Annahme ausgewiesen.
            _ => new ConditionResult(condition, Satisfied: true, IsAssumption: true)
        };
    }

    /// <summary>Mengenvergleich: Bestand gegen Operator und Sollwert (ohne Zahl: mindestens 1).</summary>
    private static ConditionResult Number(Condition condition, double actual)
    {
        var wanted = condition.NumberValue ?? 1;

        var satisfied = condition.Operator switch
        {
            ComparisonOperator.AtLeast => actual >= wanted,
            ComparisonOperator.GreaterThan => actual > wanted,
            ComparisonOperator.Equal => Math.Abs(actual - wanted) < 0.000001,
            ComparisonOperator.AtMost => actual <= wanted,
            ComparisonOperator.LessThan => actual < wanted,
            ComparisonOperator.NotEqual => Math.Abs(actual - wanted) >= 0.000001,
            _ => false
        };

        return new ConditionResult(condition, satisfied, IsAssumption: false);
    }

    /// <summary>Ja/Nein-Frage: Der Ist-Zustand muss dem gewollten entsprechen — auch „ausdrücklich nicht“.</summary>
    private static ConditionResult Boolean(Condition condition, bool actual) =>
        new(condition, actual == (condition.BooleanValue ?? true), IsAssumption: false);

    private static ConditionResult QuestState(Condition condition, GameStateAssumption state)
    {
        // Ohne gewollten Zustand ist nichts verlangt.
        if (string.IsNullOrWhiteSpace(condition.TextValue))
        {
            return new ConditionResult(condition, Satisfied: true, IsAssumption: true);
        }

        var actual = state.QuestStates.GetValueOrDefault(condition.TargetEntityId.GetValueOrDefault());

        var satisfied = actual is not null
            && string.Equals(actual.Trim(), condition.TextValue.Trim(), StringComparison.OrdinalIgnoreCase);

        return new ConditionResult(condition, satisfied, IsAssumption: false);
    }
}
