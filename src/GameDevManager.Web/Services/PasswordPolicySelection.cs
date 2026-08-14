using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Hält die Passwortrichtlinie fest — installationsweit, aus demselben Grund wie die
/// <see cref="AppearanceSelection"/>: Das Tool wird self-hosted betrieben, und eine Richtlinie
/// je Browser ergäbe keinen Sinn.
/// <para>
/// Der Startwert kommt aus der Konfiguration (<c>PasswordPolicy:*</c>, geschrieben nach
/// <c>appsettings.Local.json</c>); ohne Eintrag gilt <see cref="PasswordPolicy.Default"/>.
/// Ein Ändern schreibt die Werte zurück und gilt sofort — der <c>UserService</c> fragt bei
/// jeder Prüfung <see cref="Current"/> ab, ein Neustart ist nicht nötig.
/// </para>
/// </summary>
public class PasswordPolicySelection(IConfiguration configuration, LocalSettingsFile settings)
    : IPasswordPolicyProvider
{
    public PasswordPolicy Current { get; private set; } = ReadFrom(configuration);

    public async Task SetAsync(PasswordPolicy policy, CancellationToken ct = default)
    {
        var clamped = policy with
        {
            MinimumLength = Math.Clamp(
                policy.MinimumLength, PasswordPolicy.MinimumLengthFloor, PasswordPolicy.MinimumLengthCeiling)
        };

        Current = clamped;
        await settings.WritePasswordPolicyAsync(clamped, ct);
    }

    private static PasswordPolicy ReadFrom(IConfiguration configuration)
    {
        var fallback = PasswordPolicy.Default;

        var minimumLength = int.TryParse(configuration["PasswordPolicy:MinimumLength"], out var length)
            ? Math.Clamp(length, PasswordPolicy.MinimumLengthFloor, PasswordPolicy.MinimumLengthCeiling)
            : fallback.MinimumLength;

        return new PasswordPolicy(
            minimumLength,
            ReadBool(configuration, "PasswordPolicy:RequireDigit", fallback.RequireDigit),
            ReadBool(configuration, "PasswordPolicy:RequireSpecialCharacter", fallback.RequireSpecialCharacter),
            ReadBool(configuration, "PasswordPolicy:PasswordsDisabled", fallback.PasswordsDisabled));
    }

    private static bool ReadBool(IConfiguration configuration, string key, bool fallback) =>
        bool.TryParse(configuration[key], out var value) ? value : fallback;
}
