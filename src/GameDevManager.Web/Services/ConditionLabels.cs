using GameDevManager.Domain.Entities;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Bezeichnungen rund um das Bedingungssystem. Die Enums bleiben englisch — hier liegt allein
/// die Darstellung, und die kommt aus <c>ConditionLabels.resx</c>.
/// </summary>
public sealed class ConditionLabels(IStringLocalizer<ConditionLabels> localizer)
{
    public string Describe(ConditionKind kind) => kind switch
    {
        ConditionKind.HasItem => localizer["Kind_HasItem"],
        ConditionKind.HasCurrency => localizer["Kind_HasCurrency"],
        ConditionKind.QuestState => localizer["Kind_QuestState"],
        ConditionKind.NpcDefeated => localizer["Kind_NpcDefeated"],
        ConditionKind.Flag => localizer["Kind_Flag"],
        ConditionKind.PlayerLevel => localizer["Kind_PlayerLevel"],
        ConditionKind.TimeOfDay => localizer["Kind_TimeOfDay"],
        ConditionKind.Weather => localizer["Kind_Weather"],
        ConditionKind.Biome => localizer["Kind_Biome"],
        ConditionKind.Unlocked => localizer["Kind_Unlocked"],
        ConditionKind.Custom => localizer["Kind_Custom"],
        _ => kind.ToString()
    };

    /// <summary>Bleibt statisch: Icons sind sprachunabhängig.</summary>
    public static string Icon(ConditionKind kind) => kind switch
    {
        ConditionKind.HasItem => Icons.Material.Filled.Category,
        ConditionKind.HasCurrency => Icons.Material.Filled.Paid,
        ConditionKind.QuestState => Icons.Material.Filled.Assignment,
        ConditionKind.NpcDefeated => Icons.Material.Filled.People,
        ConditionKind.Flag => Icons.Material.Filled.Flag,
        ConditionKind.PlayerLevel => Icons.Material.Filled.TrendingUp,
        ConditionKind.TimeOfDay => Icons.Material.Filled.WbTwilight,
        ConditionKind.Weather => Icons.Material.Filled.Cloud,
        ConditionKind.Biome => Icons.Material.Filled.Terrain,
        ConditionKind.Unlocked => Icons.Material.Filled.LockOpen,
        _ => Icons.Material.Filled.HelpOutline
    };

    public string Describe(ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.AtLeast => localizer["Operator_AtLeast"],
        ComparisonOperator.GreaterThan => localizer["Operator_GreaterThan"],
        ComparisonOperator.Equal => localizer["Operator_Equal"],
        ComparisonOperator.AtMost => localizer["Operator_AtMost"],
        ComparisonOperator.LessThan => localizer["Operator_LessThan"],
        ComparisonOperator.NotEqual => localizer["Operator_NotEqual"],
        _ => comparison.ToString()
    };

    /// <summary>
    /// Die Bedingung als lesbarer Satz. <paramref name="targetName"/> ist der Name der bezogenen
    /// Entität, sofern er der aufrufenden Seite bekannt ist.
    /// </summary>
    public string Sentence(Condition condition, string? targetName)
    {
        var target = targetName ?? localizer["UnknownTarget"].Value;
        var amount = condition.NumberValue?.ToString("0.##") ?? string.Empty;

        return condition.Kind switch
        {
            ConditionKind.HasItem =>
                localizer["Sentence_HasItem", Describe(condition.Operator), amount, target],
            ConditionKind.HasCurrency =>
                localizer["Sentence_HasCurrency", Describe(condition.Operator), amount, target],
            ConditionKind.PlayerLevel =>
                localizer["Sentence_PlayerLevel", Describe(condition.Operator), amount],
            ConditionKind.NpcDefeated =>
                condition.BooleanValue == false
                    ? localizer["Sentence_NpcNotDefeated", target]
                    : localizer["Sentence_NpcDefeated", target],
            ConditionKind.Flag =>
                condition.BooleanValue == false
                    ? localizer["Sentence_FlagNotSet", condition.TextValue ?? string.Empty]
                    : localizer["Sentence_FlagSet", condition.TextValue ?? string.Empty],
            ConditionKind.QuestState =>
                localizer["Sentence_QuestState", target, condition.TextValue ?? string.Empty],
            ConditionKind.TimeOfDay or ConditionKind.Weather or ConditionKind.Biome =>
                condition.BooleanValue == false
                    ? localizer["Sentence_WorldStateNot", Describe(condition.Kind), target]
                    : localizer["Sentence_WorldState", Describe(condition.Kind), target],
            ConditionKind.Unlocked =>
                condition.BooleanValue == false
                    ? localizer["Sentence_NotUnlocked", target]
                    : localizer["Sentence_Unlocked", target],
            _ => condition.TextValue ?? localizer["Sentence_Custom"].Value
        };
    }
}
