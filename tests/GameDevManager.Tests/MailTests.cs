using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Mailversand hinter der Schnittstelle: Ohne Konfiguration ist alles ein No-Op, und
/// die Tests können die Schnittstelle durch eine Attrappe ersetzen.
/// </summary>
public class MailTests
{
    [Fact]
    public async Task Die_Vorgabe_ist_ein_stiller_No_Op()
    {
        var sender = new NullMailSender();

        Assert.False(sender.IsConfigured);

        // Schicken ohne Versandweg wirft nicht — es passiert schlicht nichts.
        await sender.SendAsync("dev@example.org", "Betreff", "Text");
    }

    [Fact]
    public void Konfiguriert_ist_der_Versand_erst_mit_Host_und_Absender()
    {
        Assert.False(new MailOptions().IsConfigured);
        Assert.False(new MailOptions { Host = "smtp.example.org" }.IsConfigured);
        Assert.False(new MailOptions { From = "gdm@example.org" }.IsConfigured);
        Assert.True(new MailOptions { Host = "smtp.example.org", From = "gdm@example.org" }.IsConfigured);
    }

    [Fact]
    public void Ohne_Web_Schicht_ist_die_Vorgabe_registriert()
    {
        using var test = new TestDatabase();

        // Der DI-Aufbau der Tests ist der der Anwendung ohne Web — dort gilt der No-Op.
        Assert.IsType<NullMailSender>(test.GetService<IMailSender>());
    }

    /// <summary>Die Attrappe, mit der spätere Tests den Versand beobachten.</summary>
    private sealed class RecordingMailSender : IMailSender
    {
        public List<(string To, string Subject)> Sent { get; } = [];

        public bool IsConfigured => true;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Die_Schnittstelle_laesst_sich_durch_eine_Attrappe_ersetzen()
    {
        var recorder = new RecordingMailSender();
        IMailSender sender = recorder;

        await sender.SendAsync("dev@example.org", "Abnahme entschieden", "…");

        Assert.Equal(("dev@example.org", "Abnahme entschieden"), Assert.Single(recorder.Sent));
    }
}
