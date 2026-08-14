using GameDevManager.Domain.Entities;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Bezeichnungen rund um das Änderungsprotokoll — dasselbe Muster wie <see cref="ConditionLabels"/>.
/// Sie liegen hier und nicht an einer Seite, weil zwei Ansichten dieselben Einträge zeigen: die
/// Protokollseite über alle Module und der Abschnitt „Geschichte“ in jeder Bearbeitungsmaske.
/// </summary>
public sealed class ChangeActionLabels(IStringLocalizer<ChangeActionLabels> localizer)
{
    public string Describe(ChangeAction action) => action switch
    {
        ChangeAction.Created => localizer["Action_Created"],
        ChangeAction.Updated => localizer["Action_Updated"],
        ChangeAction.Deleted => localizer["Action_Deleted"],
        ChangeAction.Imported => localizer["Action_Imported"],
        _ => action.ToString()
    };

    /// <summary>
    /// Die Zusatzangabe eines Eintrags als Satz. Bei einer Änderung sind das die Namen der
    /// geänderten Eigenschaften und brauchen ihre Einleitung; alles andere steht für sich.
    /// </summary>
    public string? Describe(ChangeAction action, string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        return action == ChangeAction.Updated ? localizer["ChangedFields", details] : details;
    }

    /// <summary>Bleibt statisch: Icons sind sprachunabhängig.</summary>
    public static string Icon(ChangeAction action) => action switch
    {
        ChangeAction.Created => Icons.Material.Filled.AddCircleOutline,
        ChangeAction.Updated => Icons.Material.Filled.EditNote,
        ChangeAction.Deleted => Icons.Material.Filled.DeleteOutline,
        _ => Icons.Material.Filled.CloudDownload
    };

    /// <summary>Ebenfalls sprachunabhängig — und bewusst nicht <c>Color</c> genannt, das ist der Typ.</summary>
    public static Color Accent(ChangeAction action) => action switch
    {
        ChangeAction.Created => Color.Success,
        ChangeAction.Deleted => Color.Error,
        ChangeAction.Imported => Color.Warning,
        _ => Color.Default
    };
}
