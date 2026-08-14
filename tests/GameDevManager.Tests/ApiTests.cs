using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die lesende HTTP-API: Schlüssel und Inhalte. Geprüft wird vor allem, was einen Schlüssel
/// <b>ungültig</b> macht — ein Schlüssel, der zu lange gilt, ist das eigentliche Risiko.
/// </summary>
public class ApiTests
{
    private static async Task<Guid> SeedItemAsync(TestDatabase database, string name)
    {
        await using var db = database.CreateContext();

        var item = new Item { GameProjectId = database.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    [Fact]
    public async Task Ein_frischer_Schluessel_gilt_und_steht_nur_einmal_im_Klartext()
    {
        using var database = new TestDatabase();
        var keys = database.GetService<ApiKeyService>();

        var created = await keys.CreateAsync("Unity-Plugin", null, null);

        Assert.StartsWith(ApiKeyService.Prefix, created.PlainText);
        // In der Datenbank steht nur der Hash — der Klartext darf dort nirgends auftauchen.
        Assert.DoesNotContain(created.PlainText, created.Key.KeyHash);

        var validated = await keys.ValidateAsync(created.PlainText);

        Assert.NotNull(validated);
        Assert.Equal(created.Key.Id, validated!.Id);
        Assert.NotNull(validated.LastUsedAtUtc);
    }

    [Fact]
    public async Task Ein_falscher_ein_gesperrter_und_ein_abgelaufener_Schluessel_kommen_nicht_herein()
    {
        using var database = new TestDatabase();
        var keys = database.GetService<ApiKeyService>();

        Assert.Null(await keys.ValidateAsync("gdm_unsinn"));
        Assert.Null(await keys.ValidateAsync(null));

        var blocked = await keys.CreateAsync("Gesperrt", null, null);
        await keys.SetDisabledAsync(blocked.Key.Id, disabled: true);
        Assert.Null(await keys.ValidateAsync(blocked.PlainText));

        var expired = await keys.CreateAsync("Abgelaufen", null, DateTime.UtcNow.AddDays(-1));
        Assert.Null(await keys.ValidateAsync(expired.PlainText));
    }

    [Fact]
    public async Task Schluessel_verwalten_darf_nur_ein_Verwalter()
    {
        using var database = new TestDatabase();
        var keys = database.GetService<ApiKeyService>();

        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false };

        await Assert.ThrowsAsync<ContentValidationException>(() => keys.CreateAsync("Heimlich", null, null));
        await Assert.ThrowsAsync<ContentValidationException>(() => keys.GetKeysAsync());
    }

    [Fact]
    public async Task Die_API_liefert_ein_Modul_samt_Arten_und_Feldwerten()
    {
        using var database = new TestDatabase();
        var itemId = await SeedItemAsync(database, "Schwert");

        var payload = await database.GetService<ContentApiService>()
            .GetModuleAsync(database.ProjectId, ModuleKeys.Items, null);

        Assert.NotNull(payload);

        // Serialisiert wird mit denselben Regeln wie der Export — wer das ZIP lesen kann,
        // kann auch das hier lesen.
        var json = System.Text.Json.JsonSerializer.Serialize(payload, ContentApiService.JsonOptions);

        Assert.Contains("\"moduleKey\"", json);
        Assert.Contains("\"items\"", json);
        Assert.Contains(itemId.ToString(), json);
        Assert.Contains("Schwert", json);
    }

    [Fact]
    public async Task Ein_unbekanntes_Modul_und_ein_unbekannter_Eintrag_ergeben_nichts()
    {
        using var database = new TestDatabase();
        var api = database.GetService<ContentApiService>();

        Assert.Null(await api.GetModuleAsync(database.ProjectId, "gibtsnicht", null));
        Assert.Null(await api.GetEntityAsync(database.ProjectId, ModuleKeys.Items, Guid.NewGuid()));
    }

    [Fact]
    public async Task Mit_Sprache_liefert_die_API_die_Uebersetzungen_mit()
    {
        using var database = new TestDatabase();
        var itemId = await SeedItemAsync(database, "Schwert");

        var localization = database.GetService<LocalizationService>();
        await localization.SaveLanguageAsync(database.ProjectId, new ContentLanguage { Code = "de", Name = "Deutsch" });
        await localization.SaveLanguageAsync(database.ProjectId, new ContentLanguage { Code = "en", Name = "Englisch" });
        await localization.SaveAsync(
            database.ProjectId, itemId, ModuleKeys.Items, TranslationSlots.Name, "en", "Sword", "Schwert");

        var payload = await database.GetService<ContentApiService>()
            .GetModuleAsync(database.ProjectId, ModuleKeys.Items, "en");

        var json = System.Text.Json.JsonSerializer.Serialize(payload, ContentApiService.JsonOptions);

        Assert.Contains("Sword", json);
    }

    [Fact]
    public async Task Ein_Eintrag_aus_einem_fremden_Projekt_wird_nicht_geliefert()
    {
        using var database = new TestDatabase();
        var itemId = await SeedItemAsync(database, "Schwert");

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);

        // Die Projektgrenze steht in der Abfrage — eine untergeschobene GUID ändert daran nichts.
        Assert.Null(await database.GetService<ContentApiService>()
            .GetEntityAsync(other.Id, ModuleKeys.Items, itemId));
    }
}
