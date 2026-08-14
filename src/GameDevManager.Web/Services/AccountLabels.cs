using Microsoft.Extensions.Localization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Texte rund um Anmeldung und Benutzer, die aus C# statt aus einer Razor-Datei kommen —
/// derselbe Umweg wie bei <see cref="ModuleLabels"/> und <see cref="ConditionLabels"/>.
/// </summary>
public sealed class AccountLabels(IStringLocalizer<AccountLabels> localizer) : ISystemUserName
{
    /// <summary>Der Name im Änderungsprotokoll, wenn ohne Anmeldung gearbeitet wurde.</summary>
    public string Name => localizer["SystemUser"];
}
