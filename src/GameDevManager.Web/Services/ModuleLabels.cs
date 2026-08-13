using Microsoft.Extensions.Localization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Name und Beschreibung eines Moduls. Sie stehen nicht mehr in <see cref="ModuleDefinition"/>,
/// weil die Registry statisch ist und einen Localizer zur Feldinitialisierung nicht kennen kann.
/// Die Schlüssel sind <c>&lt;ModuleKey&gt;_Name</c> und <c>&lt;ModuleKey&gt;_Description</c>.
/// </summary>
public sealed class ModuleLabels(IStringLocalizer<ModuleLabels> localizer)
{
    public string Name(ModuleDefinition module) => Name(module.Id);

    /// <summary>Fällt auf den Schlüssel zurück, damit unbekannte Module nicht leer erscheinen.</summary>
    public string Name(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return string.Empty;
        }

        var localized = localizer[$"{moduleKey}_Name"];
        return localized.ResourceNotFound ? moduleKey : localized.Value;
    }

    public string Description(ModuleDefinition module) => localizer[$"{module.Id}_Description"];

    /// <summary>Name des Moduls zu einem Schlüssel, der auch unbekannt sein darf.</summary>
    public string NameOrKey(string? moduleKey) => Name(moduleKey ?? string.Empty);

    /// <summary>Name eines Arbeitsfelds — die Überschriften im Inhaltsbestand des Dashboards.</summary>
    public string GroupName(ModuleGroup group) => localizer[$"Group_{group}_Name"];
}
