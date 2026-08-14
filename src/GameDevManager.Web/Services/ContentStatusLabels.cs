using GameDevManager.Domain.Entities;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace GameDevManager.Web.Services;

/// <summary>
/// Bezeichnungen des Bearbeitungsstands — dasselbe Muster wie <see cref="ChangeActionLabels"/>
/// und <see cref="ConditionLabels"/>. Sie liegen hier und nicht an einer Seite, weil derselbe
/// Stand an vielen Stellen erscheint: in jeder Bearbeitungsmaske, in jeder Modul-Liste, in der
/// globalen Suche, im Dashboard-Band und auf der Export-Seite.
/// </summary>
public sealed class ContentStatusLabels(IStringLocalizer<ContentStatusLabels> localizer)
{
    /// <summary>Die Stände in ihrer natürlichen Reihenfolge — vom Entwurf zum Fertigen.</summary>
    public static IReadOnlyList<ContentStatus> All { get; } =
        [ContentStatus.Draft, ContentStatus.InProgress, ContentStatus.InReview, ContentStatus.Done];

    public string Describe(ContentStatus status) => status switch
    {
        ContentStatus.Draft => localizer["Status_Draft"],
        ContentStatus.InProgress => localizer["Status_InProgress"],
        ContentStatus.InReview => localizer["Status_InReview"],
        ContentStatus.Done => localizer["Status_Done"],
        _ => status.ToString()
    };

    /// <summary>Bleibt statisch: Icons sind sprachunabhängig.</summary>
    public static string Icon(ContentStatus status) => status switch
    {
        ContentStatus.Draft => Icons.Material.Filled.EditNote,
        ContentStatus.InProgress => Icons.Material.Filled.HourglassEmpty,
        ContentStatus.InReview => Icons.Material.Filled.RateReview,
        _ => Icons.Material.Filled.CheckCircleOutline
    };

    /// <summary>
    /// Ebenfalls sprachunabhängig — und bewusst nicht <c>Color</c> genannt, das ist der Typ.
    /// Der Entwurf bleibt farblos: Er ist der Normalzustand und soll nicht wie ein Fund
    /// aussehen; nur das Fertige bekommt den Akzent.
    /// </summary>
    public static Color Accent(ContentStatus status) => status switch
    {
        ContentStatus.InProgress => Color.Info,
        ContentStatus.InReview => Color.Warning,
        ContentStatus.Done => Color.Success,
        _ => Color.Default
    };
}
