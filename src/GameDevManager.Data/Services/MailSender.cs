namespace GameDevManager.Data.Services;

/// <summary>
/// Die SMTP-Konfiguration der Installation — <c>Mail:*</c> in den appsettings, dasselbe
/// Muster wie die Passwortrichtlinie: Konfiguration statt Datenbanktabelle, denn sie
/// beschreibt die Installation, nicht ein Projekt. Ohne <see cref="Host"/> und
/// <see cref="From"/> ist der Versand abgeschaltet, und alles, was Mails schicken will,
/// läuft ins Leere statt in einen Fehler.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>TLS beim Verbinden — die Vorgabe; wer einen offenen Test-SMTP fährt, schaltet ab.</summary>
    public bool UseSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Die Absenderadresse — ohne sie nimmt kein SMTP-Server die Mail an.</summary>
    public string? From { get; set; }

    /// <summary>
    /// Der Takt des Digests in Minuten. Gebündelt statt je Ereignis — wer zwanzig Karten
    /// zuweist, löst eine Mail aus, keinen Sturm.
    /// </summary>
    public int DigestMinutes { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>
/// Der Mailversand als Schnittstelle der Datenschicht — die Umsetzung (SMTP) liegt in der
/// Web-Schicht, wie beim Webhook-Versand: ausgehende Verbindungen gehören nicht neben die
/// Datenbankzugriffe. Die Tests ersetzen die Schnittstelle durch eine Attrappe.
/// </summary>
public interface IMailSender
{
    /// <summary>Ob überhaupt ein Versandweg eingerichtet ist — die Oberfläche erklärt sonst, warum nichts ankommt.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Schickt eine Mail. Ein Fehlschlag wirft nicht — eine Benachrichtigung, die nicht
    /// ankommt, darf den Vorgang nicht aufhalten, der sie ausgelöst hat.
    /// </summary>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Die Vorgabe ohne Konfiguration: ein No-Op. Tests und Installationen ohne SMTP bekommen
/// damit denselben stillen Weg — dieselbe Bauart wie <c>SystemChangeAuthorProvider</c>.
/// </summary>
public sealed class NullMailSender : IMailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) =>
        Task.CompletedTask;
}
