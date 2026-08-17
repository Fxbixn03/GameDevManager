using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Webhooks (F37): Bei Änderungen wird eine Adresse aufgerufen, damit ein Build-Server den
/// Export von selbst abholt. Eingereiht wird im <c>ChangeLogInterceptor</c> — er sieht jede
/// Änderung ohnehin —, zugestellt aus dem Hintergrunddienst: Eine hängende HTTP-Anfrage darf
/// keine Transaktion aufhalten.
/// </summary>
public class WebhookTests
{
    private static Webhook Draft(TestDatabase test, string? modules = null) => new()
    {
        GameProjectId = test.ProjectId,
        Name = "Build-Server",
        Url = "https://build.example/hook",
        ModuleKeys = modules
    };

    [Fact]
    public async Task Eine_kaputte_Adresse_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var hooks = test.GetService<WebhookService>();

        var draft = Draft(test);
        draft.Url = "ftp://build.example/hook";

        // Ein anderes Schema wäre kein Aufruf, sondern ein Weg, den Server dazu zu bringen,
        // etwas anderes zu tun.
        await Assert.ThrowsAsync<ContentValidationException>(() => hooks.SaveWebhookAsync(draft));

        draft.Url = "kein-url";
        await Assert.ThrowsAsync<ContentValidationException>(() => hooks.SaveWebhookAsync(draft));
    }

    [Fact]
    public async Task Ein_Webhook_laesst_sich_anlegen_aendern_und_loeschen()
    {
        using var test = new TestDatabase();
        var hooks = test.GetService<WebhookService>();

        var draft = Draft(test);
        await hooks.SaveWebhookAsync(draft);

        var stored = Assert.Single(await hooks.GetWebhooksAsync(test.ProjectId));
        Assert.Equal("Build-Server", stored.Name);

        draft.Name = "Anderer Server";
        await hooks.SaveWebhookAsync(draft);

        Assert.Equal("Anderer Server", Assert.Single(await hooks.GetWebhooksAsync(test.ProjectId)).Name);

        await hooks.DeleteWebhookAsync(draft.Id);
        Assert.Empty(await hooks.GetWebhooksAsync(test.ProjectId));
    }

    [Fact]
    public void Ohne_Filter_hoert_ein_Webhook_auf_alles()
    {
        var hook = new Webhook { GameProjectId = Guid.NewGuid(), Name = "H", Url = "https://x" };

        Assert.True(WebhookService.Listens(hook, ModuleKeys.Items));
        Assert.True(WebhookService.Listens(hook, ModuleKeys.Npcs));
    }

    [Fact]
    public void Mit_Filter_hoert_er_nur_auf_die_genannten()
    {
        var hook = new Webhook
        {
            GameProjectId = Guid.NewGuid(),
            Name = "H",
            Url = "https://x",
            ModuleKeys = "items, npcs"
        };

        Assert.True(WebhookService.Listens(hook, ModuleKeys.Items));
        Assert.True(WebhookService.Listens(hook, ModuleKeys.Npcs));
        Assert.False(WebhookService.Listens(hook, ModuleKeys.Quests));
    }

    [Fact]
    public async Task Eine_Aenderung_landet_in_der_Warteschlange()
    {
        using var test = new TestDatabase();
        var queue = test.GetService<WebhookQueue>();

        // Ohne Empfänger wird gar nicht erst eingereiht — das hält das Speichern frei von
        // einer Schlange, die niemand leert.
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Ungehört";
        await items.SaveItemAsync(context);

        Assert.Empty(queue.DrainAll());

        // Mit Empfänger schon.
        await test.GetService<WebhookService>().SaveWebhookAsync(Draft(test));

        var second = await items.LoadForEditAsync(test.ProjectId, null);
        second!.Entity.Name = "Gehört";
        await items.SaveItemAsync(second);

        var pending = queue.DrainAll();
        var entry = Assert.Single(pending);

        Assert.Equal(ModuleKeys.Items, entry.ModuleKey);
        Assert.Equal("Gehört", entry.EntityName);
        Assert.Equal(ChangeAction.Created, entry.Action);

        // Und die Schlange ist danach leer — der Dienst nimmt heraus, statt zu kopieren.
        Assert.Empty(queue.DrainAll());
    }

    [Fact]
    public void Die_Warteschlange_haelt_ihre_Obergrenze_ein()
    {
        var queue = new WebhookQueue { HasSubscribers = true };
        var projectId = Guid.NewGuid();

        for (var index = 0; index < 1200; index++)
        {
            queue.Enqueue(new WebhookEvent(
                projectId, ModuleKeys.Items, Guid.NewGuid(), $"Item {index}",
                ChangeAction.Created, "test", DateTime.UtcNow));
        }

        var drained = queue.DrainAll();

        // Steht der Empfänger still, während jemand einen Import fährt, wüchse sie sonst
        // unbegrenzt. Ältestes fällt zuerst — die jüngere Nachricht ist die aktuellere.
        Assert.Equal(1000, drained.Count);
        Assert.Equal("Item 1199", drained[^1].EntityName);
    }

    [Fact]
    public async Task Das_Ergebnis_eines_Versuchs_wird_festgehalten()
    {
        using var test = new TestDatabase();
        var hooks = test.GetService<WebhookService>();

        var draft = Draft(test);
        await hooks.SaveWebhookAsync(draft);

        await hooks.RecordDeliveryAsync(draft.Id, 500, "Interner Serverfehler");

        var stored = Assert.Single(await hooks.GetWebhooksAsync(test.ProjectId));

        Assert.Equal(500, stored.LastStatusCode);
        Assert.Equal("Interner Serverfehler", stored.LastError);
        Assert.NotNull(stored.LastDeliveryAtUtc);
    }
}
