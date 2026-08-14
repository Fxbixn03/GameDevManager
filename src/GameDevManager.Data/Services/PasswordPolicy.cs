namespace GameDevManager.Data.Services;

/// <summary>
/// Die Passwortrichtlinie der Installation: Mindestlänge, Pflicht auf Ziffer und Sonderzeichen —
/// oder Passwörter ganz aus, dann meldet der Anmeldename allein an.
/// <para>
/// Sie hängt wie die Hell/Dunkel-Wahl an der Installation und nicht an einem Projekt oder in der
/// Datenbank: eine Handvoll Werte, die die Web-Schicht in <c>appsettings.Local.json</c> ablegt —
/// eine Tabelle dafür hieße eine Migration in allen vier Providern. Die Datenschicht kennt nur
/// die Schnittstelle <see cref="IPasswordPolicyProvider"/>, dasselbe Muster wie beim
/// <c>IChangeAuthorProvider</c>.
/// </para>
/// </summary>
public sealed record PasswordPolicy(
    int MinimumLength,
    bool RequireDigit,
    bool RequireSpecialCharacter,
    bool PasswordsDisabled)
{
    /// <summary>Kürzer nimmt die Vorgabe ein Passwort nicht an.</summary>
    public const int DefaultMinimumLength = 8;

    /// <summary>Grenzen der Mindestlänge — PBKDF2 verkraftet mehr, aber niemand tippt es.</summary>
    public const int MinimumLengthFloor = 1;
    public const int MinimumLengthCeiling = 128;

    /// <summary>Die Vorgabe ohne Eintrag in der Konfiguration: 8 Zeichen, keine weiteren Pflichten.</summary>
    public static readonly PasswordPolicy Default = new(
        DefaultMinimumLength, RequireDigit: false, RequireSpecialCharacter: false, PasswordsDisabled: false);
}

/// <summary>
/// Woher die aktuelle Richtlinie kommt. Im Betrieb beantwortet das die Web-Schicht
/// (Konfiguration plus Einstellungsseite); ohne Ersatz gilt die Vorgabe.
/// </summary>
public interface IPasswordPolicyProvider
{
    PasswordPolicy Current { get; }
}

/// <summary>Die Vorgabe-Richtlinie — für Betrieb ohne Web-Schicht und als Ausgangspunkt der Tests.</summary>
public sealed class DefaultPasswordPolicyProvider : IPasswordPolicyProvider
{
    public PasswordPolicy Current => PasswordPolicy.Default;
}
