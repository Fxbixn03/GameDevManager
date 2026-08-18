using System.Net;
using System.Net.Mail;
using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Der SMTP-Versand hinter <see cref="IMailSender"/>.
/// <para>
/// <b>Bewusst über <see cref="SmtpClient"/> und nicht über MailKit</b> — dieselbe Abwägung
/// wie beim <c>Csv</c> und beim <c>ImageDimensionReader</c>: <see cref="SmtpClient"/> steckt
/// im Framework und kostet keine Abhängigkeit; verschickt werden kurze Benachrichtigungen an
/// den SMTP-Server der eigenen Installation. Die bekannten Schwächen der Klasse (kein OAuth,
/// keine modernen Auth-Verfahren) treffen genau diesen Fall nicht — und sollte er wachsen,
/// ist die Schnittstelle die Naht, an der MailKit einzusetzen wäre, ohne dass ein Aufrufer
/// etwas merkt.
/// </para>
/// <para>
/// Ein Fehlschlag landet im Log und wirft nicht: Eine Benachrichtigung, die nicht ankommt,
/// darf den Vorgang nicht aufhalten, der sie ausgelöst hat — dieselbe Linie wie beim
/// Webhook-Versand.
/// </para>
/// </summary>
public sealed class SmtpMailSender(MailOptions options, ILogger<SmtpMailSender> log) : IMailSender
{
    public bool IsConfigured => options.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // Ohne Konfiguration ein No-Op — der Aufrufer muss nicht selbst nachfragen.
        if (!IsConfigured || string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        try
        {
            using var client = new SmtpClient(options.Host!, options.Port)
            {
                EnableSsl = options.UseSsl
            };

            if (!string.IsNullOrWhiteSpace(options.UserName))
            {
                client.Credentials = new NetworkCredential(options.UserName, options.Password);
            }

            using var message = new MailMessage(options.From!, to, subject, body);

            await client.SendMailAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail an {Recipient} konnte nicht verschickt werden.", to);
        }
    }
}
