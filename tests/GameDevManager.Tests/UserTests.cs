using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Benutzer und Passwörter — die Grundlage des Änderungsprotokolls. Geprüft wird das Hashing,
/// die Anmeldung und der Schutz des letzten Verwalters.
/// </summary>
public class UserTests
{
    [Fact]
    public void Ein_Passwort_wird_gehasht_und_nie_im_Klartext_abgelegt()
    {
        var hash = PasswordHasher.Hash("Geheim1234");

        Assert.DoesNotContain("Geheim1234", hash);
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.True(PasswordHasher.Verify("Geheim1234", hash));
        Assert.False(PasswordHasher.Verify("geheim1234", hash));
    }

    [Fact]
    public void Zweimal_dasselbe_Passwort_ergibt_zwei_verschiedene_Hashes()
    {
        // Jeder Hash bekommt sein eigenes Salz — sonst verriete ein Blick in die Tabelle,
        // welche Konten dasselbe Passwort haben.
        Assert.NotEqual(PasswordHasher.Hash("Geheim1234"), PasswordHasher.Hash("Geheim1234"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein-hash")]
    [InlineData("pbkdf2-sha256$abc$xx$yy")]
    public void Ein_unlesbarer_Hash_laesst_niemanden_herein(string? stored)
    {
        // Ein beschädigter Datensatz soll die Anmeldeseite nicht zerlegen, sondern den
        // Benutzer nicht hereinlassen.
        Assert.False(PasswordHasher.Verify("beliebig", stored));
    }

    [Fact]
    public async Task Der_erste_Benutzer_wird_immer_Verwalter()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        Assert.False(await users.HasAnyUserAsync());

        var id = await users.CreateUserAsync("fabian", "Fabian", "Geheim1234", isAdministrator: false);

        var created = await users.GetUserAsync(id);
        Assert.NotNull(created);
        Assert.True(created.IsAdministrator);
        Assert.True(await users.HasAnyUserAsync());
    }

    [Fact]
    public async Task Anmeldenamen_gibt_es_nur_einmal()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        await users.CreateUserAsync("fabian", "Fabian", "Geheim1234", isAdministrator: true);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.CreateUserAsync("Fabian", "Zweiter", "Geheim1234", isAdministrator: false));
    }

    [Fact]
    public async Task Zu_kurze_Passwoerter_werden_abgelehnt()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.CreateUserAsync("kurz", "Kurz", "abc", isAdministrator: true));
    }

    [Fact]
    public async Task Die_Anmeldung_prueft_Passwort_und_Sperre()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        await users.CreateUserAsync("admin", "Verwalter", "Geheim1234", isAdministrator: true);
        var id = await users.CreateUserAsync("gast", "Gast", "Geheim1234", isAdministrator: false);

        Assert.NotNull(await users.AuthenticateAsync("gast", "Geheim1234"));
        Assert.Null(await users.AuthenticateAsync("gast", "falsch"));
        Assert.Null(await users.AuthenticateAsync("gibtesnicht", "Geheim1234"));

        await users.UpdateUserAsync(id, "Gast", isAdministrator: false, isDisabled: true);
        Assert.Null(await users.AuthenticateAsync("gast", "Geheim1234"));
    }

    [Fact]
    public async Task Die_Anmeldung_haelt_den_Zeitpunkt_fest()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var id = await users.CreateUserAsync("fabian", "Fabian", "Geheim1234", isAdministrator: true);
        Assert.Null((await users.GetUserAsync(id))!.LastLoginAtUtc);

        await users.AuthenticateAsync("fabian", "Geheim1234");

        Assert.NotNull((await users.GetUserAsync(id))!.LastLoginAtUtc);
    }

    [Fact]
    public async Task Der_letzte_Verwalter_kann_sich_weder_entmachten_noch_sperren_noch_loeschen()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var admin = await users.CreateUserAsync("admin", "Verwalter", "Geheim1234", isAdministrator: true);
        await users.CreateUserAsync("gast", "Gast", "Geheim1234", isAdministrator: false);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.UpdateUserAsync(admin, "Verwalter", isAdministrator: false, isDisabled: false));

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.UpdateUserAsync(admin, "Verwalter", isAdministrator: true, isDisabled: true));

        await Assert.ThrowsAsync<ContentValidationException>(() => users.DeleteUserAsync(admin));

        // Mit einem zweiten Verwalter geht es.
        await users.CreateUserAsync("admin2", "Zweiter", "Geheim1234", isAdministrator: true);
        await users.UpdateUserAsync(admin, "Verwalter", isAdministrator: false, isDisabled: false);

        Assert.False((await users.GetUserAsync(admin))!.IsAdministrator);
    }

    [Fact]
    public async Task Das_eigene_Passwort_aendert_man_nur_gegen_das_alte()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var id = await users.CreateUserAsync("fabian", "Fabian", "Geheim1234", isAdministrator: true);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.ChangeOwnPasswordAsync(id, "falsch", "NeuesGeheim1"));

        await users.ChangeOwnPasswordAsync(id, "Geheim1234", "NeuesGeheim1");

        Assert.Null(await users.AuthenticateAsync("fabian", "Geheim1234"));
        Assert.NotNull(await users.AuthenticateAsync("fabian", "NeuesGeheim1"));
    }
}
