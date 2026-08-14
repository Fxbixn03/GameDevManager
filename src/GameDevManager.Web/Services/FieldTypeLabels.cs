using GameDevManager.Domain.Curves;
using GameDevManager.Domain.Entities;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Bezeichnungen und Icons für die Feldtypen. Der Enum selbst bleibt englisch — hier liegt
/// allein die Darstellung, und die kommt aus <c>FieldTypeLabels.resx</c>.
/// </summary>
public sealed class FieldTypeLabels(IStringLocalizer<FieldTypeLabels> localizer)
{
    public string Describe(ContentFieldType type) => type switch
    {
        ContentFieldType.Text => localizer["Type_Text"],
        ContentFieldType.MultilineText => localizer["Type_MultilineText"],
        ContentFieldType.Integer => localizer["Type_Integer"],
        ContentFieldType.Decimal => localizer["Type_Decimal"],
        ContentFieldType.Boolean => localizer["Type_Boolean"],
        ContentFieldType.Date => localizer["Type_Date"],
        ContentFieldType.Select => localizer["Type_Select"],
        ContentFieldType.EntityReference => localizer["Type_EntityReference"],
        ContentFieldType.Color => localizer["Type_Color"],
        ContentFieldType.Rarity => localizer["Type_Rarity"],
        ContentFieldType.Curve => localizer["Type_Curve"],
        _ => type.ToString()
    };

    /// <summary>Bleibt statisch: Icons sind sprachunabhängig.</summary>
    public static string Icon(ContentFieldType type) => type switch
    {
        ContentFieldType.Text => Icons.Material.Filled.ShortText,
        ContentFieldType.MultilineText => Icons.Material.Filled.Notes,
        ContentFieldType.Integer => Icons.Material.Filled.Tag,
        ContentFieldType.Decimal => Icons.Material.Filled.Calculate,
        ContentFieldType.Boolean => Icons.Material.Filled.ToggleOn,
        ContentFieldType.Date => Icons.Material.Filled.CalendarMonth,
        ContentFieldType.Select => Icons.Material.Filled.ArrowDropDownCircle,
        ContentFieldType.EntityReference => Icons.Material.Filled.Link,
        ContentFieldType.Color => Icons.Material.Filled.Palette,
        ContentFieldType.Rarity => Icons.Material.Filled.Diamond,
        ContentFieldType.Curve => Icons.Material.Filled.ShowChart,
        _ => Icons.Material.Filled.HelpOutline
    };

    /// <summary>Kurzfassung eines Feldwerts für Listen und die Übersicht.</summary>
    public string Format(FieldDefinition definition, FieldValue? value)
    {
        var empty = localizer["Empty"].Value;

        if (value is null || value.IsEmpty)
        {
            return empty;
        }

        var formatted = definition.Type switch
        {
            ContentFieldType.Boolean => value.BooleanValue == true ? localizer["Yes"].Value : localizer["No"].Value,
            ContentFieldType.Date => value.DateValue?.ToString("dd.MM.yyyy") ?? empty,
            ContentFieldType.Integer => value.NumberValue?.ToString("0") ?? empty,
            ContentFieldType.Decimal => value.NumberValue?.ToString("0.##") ?? empty,
            ContentFieldType.Select => definition.Options
                .FirstOrDefault(option => option.Id == value.OptionId)?.Label ?? empty,
            ContentFieldType.EntityReference or ContentFieldType.Rarity => value.ReferenceValue?.ToString() ?? empty,
            // Die Kurve steht als JSON im Textwert — roh wäre sie in einer Liste unlesbar.
            ContentFieldType.Curve => CurveDefinition.Parse(value.TextValue)?
                .Describe(localizer["CurveTable"].Value) ?? empty,
            _ => value.TextValue ?? empty
        };

        return string.IsNullOrWhiteSpace(definition.Unit) || formatted == empty
            ? formatted
            : localizer["WithUnit", formatted, definition.Unit];
    }
}
