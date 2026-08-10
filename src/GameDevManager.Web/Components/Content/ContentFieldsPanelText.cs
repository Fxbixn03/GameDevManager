namespace GameDevManager.Web.Components.Content;

/// <summary>
/// Markertyp für die Texte von <c>ContentFieldsPanel</c>.
/// <para>
/// Die Komponente ist generisch (<c>@typeparam TEntity</c>). Ein
/// <c>IStringLocalizer&lt;ContentFieldsPanel&lt;TEntity&gt;&gt;</c> sucht seine Ressourcen unter
/// dem gemangelten Typnamen (<c>ContentFieldsPanel`1</c>) und findet die .resx deshalb nicht —
/// die Schlüssel stünden roh in der Oberfläche. Dieser nicht-generische Typ gibt dem Localizer
/// einen auflösbaren Namen; die Datei heißt entsprechend <c>ContentFieldsPanelText.resx</c>.
/// </para>
/// </summary>
public sealed class ContentFieldsPanelText;
