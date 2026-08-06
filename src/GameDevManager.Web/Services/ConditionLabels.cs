using GameDevManager.Domain.Entities;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Deutsche Bezeichnungen rund um das Bedingungssystem. Die Enums bleiben englisch —
/// hier liegt allein die Darstellung.
/// </summary>
public static class ConditionLabels
{
    public static string Describe(ConditionKind kind) => kind switch
    {
        ConditionKind.HasItem => "Item im Besitz",
        ConditionKind.HasCurrency => "Währung im Besitz",
        ConditionKind.QuestState => "Quest-Zustand",
        ConditionKind.NpcDefeated => "NPC besiegt",
        ConditionKind.Flag => "Schalter der Story",
        ConditionKind.PlayerLevel => "Stufe des Spielers",
        ConditionKind.Custom => "Frei beschrieben",
        _ => kind.ToString()
    };

    public static string Icon(ConditionKind kind) => kind switch
    {
        ConditionKind.HasItem => Icons.Material.Filled.Category,
        ConditionKind.HasCurrency => Icons.Material.Filled.Paid,
        ConditionKind.QuestState => Icons.Material.Filled.Assignment,
        ConditionKind.NpcDefeated => Icons.Material.Filled.People,
        ConditionKind.Flag => Icons.Material.Filled.Flag,
        ConditionKind.PlayerLevel => Icons.Material.Filled.TrendingUp,
        _ => Icons.Material.Filled.HelpOutline
    };

    public static string Describe(ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.AtLeast => "mindestens",
        ComparisonOperator.GreaterThan => "mehr als",
        ComparisonOperator.Equal => "genau",
        ComparisonOperator.AtMost => "höchstens",
        ComparisonOperator.LessThan => "weniger als",
        ComparisonOperator.NotEqual => "nicht",
        _ => comparison.ToString()
    };

    /// <summary>
    /// Die Bedingung als lesbarer Satz. <paramref name="targetName"/> ist der Name der bezogenen
    /// Entität, sofern er der aufrufenden Seite bekannt ist.
    /// </summary>
    public static string Sentence(Condition condition, string? targetName)
    {
        var target = targetName ?? "(unbekannt)";

        return condition.Kind switch
        {
            ConditionKind.HasItem =>
                $"Besitzt {Describe(condition.Operator)} {condition.NumberValue:0.##}× {target}",
            ConditionKind.HasCurrency =>
                $"Besitzt {Describe(condition.Operator)} {condition.NumberValue:0.##} {target}",
            ConditionKind.PlayerLevel =>
                $"Stufe {Describe(condition.Operator)} {condition.NumberValue:0.##}",
            ConditionKind.NpcDefeated =>
                condition.BooleanValue == false ? $"{target} wurde nicht besiegt" : $"{target} wurde besiegt",
            ConditionKind.Flag =>
                condition.BooleanValue == false
                    ? $"Schalter „{condition.TextValue}“ ist nicht gesetzt"
                    : $"Schalter „{condition.TextValue}“ ist gesetzt",
            ConditionKind.QuestState =>
                $"Quest {target} steht auf „{condition.TextValue}“",
            _ => condition.TextValue ?? "Frei beschriebene Bedingung"
        };
    }
}
