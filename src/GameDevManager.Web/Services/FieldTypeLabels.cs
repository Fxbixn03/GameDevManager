using GameDevManager.Domain.Entities;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Deutsche Bezeichnungen und Icons für die Feldtypen. Der Enum selbst bleibt englisch —
/// hier liegt allein die Darstellung.
/// </summary>
public static class FieldTypeLabels
{
    public static string Describe(ContentFieldType type) => type switch
    {
        ContentFieldType.Text => "Text",
        ContentFieldType.MultilineText => "Text, mehrzeilig",
        ContentFieldType.Integer => "Ganze Zahl",
        ContentFieldType.Decimal => "Kommazahl",
        ContentFieldType.Boolean => "Ja/Nein",
        ContentFieldType.Date => "Datum",
        ContentFieldType.Select => "Auswahl",
        ContentFieldType.EntityReference => "Referenz auf Entität",
        ContentFieldType.Color => "Farbe",
        _ => type.ToString()
    };

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
        _ => Icons.Material.Filled.HelpOutline
    };

    /// <summary>Kurzfassung eines Feldwerts für Listen und die Übersicht.</summary>
    public static string Format(FieldDefinition definition, FieldValue? value)
    {
        if (value is null || value.IsEmpty)
        {
            return "—";
        }

        var formatted = definition.Type switch
        {
            ContentFieldType.Boolean => value.BooleanValue == true ? "Ja" : "Nein",
            ContentFieldType.Date => value.DateValue?.ToString("dd.MM.yyyy") ?? "—",
            ContentFieldType.Integer => value.NumberValue?.ToString("0") ?? "—",
            ContentFieldType.Decimal => value.NumberValue?.ToString("0.##") ?? "—",
            ContentFieldType.Select => definition.Options
                .FirstOrDefault(option => option.Id == value.OptionId)?.Label ?? "—",
            ContentFieldType.EntityReference => value.ReferenceValue?.ToString() ?? "—",
            _ => value.TextValue ?? "—"
        };

        return string.IsNullOrWhiteSpace(definition.Unit) || formatted == "—"
            ? formatted
            : $"{formatted} {definition.Unit}";
    }
}
