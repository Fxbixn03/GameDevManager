using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Anmeldung über einen externen Anbieter (F46). Die Anmeldung selbst bleibt das Cookie — der
/// Anbieter beweist nur, wer da ist; angelegt wird dabei niemand.
/// </summary>
public class ExternalLoginTests
{
    private static Task<Guid> SeedUserAsync(TestDatabase test, string name = "alrik") =>
        test.GetService<UserService>().CreateUserAsync(name, name, "Geheim!1", isAdministrator: true);

    [Fact]
    public async Task Ohne_verknuepftes_Konto_kommt_niemand_herein()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);

        // Sonst käme jeder herein, der beim Anbieter ein Konto hat, und das Tool wäre nur so
        // geschlossen wie die offenste Registrierung des Anbieters.
        Assert.Null(await test.GetService<UserService>().AuthenticateExternalAsync("sub-12345"));
    }

    [Fact]
    public async Task Ein_verknuepftes_Konto_meldet_sich_an()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        await users.LinkExternalAsync(userId, "sub-12345");

        var user = await users.AuthenticateExternalAsync("sub-12345");

        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal("sub-12345", user.ExternalId);
        Assert.NotNull(user.LastLoginAtUtc);
    }

    [Fact]
    public async Task Zwei_Konten_teilen_sich_keinen_Bezeichner()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var first = await SeedUserAsync(test, "alrik");
        var second = await SeedUserAsync(test, "brida");

        await users.LinkExternalAsync(first, "sub-12345");

        // Sonst wäre beim Anmelden nicht zu entscheiden, welches Konto gemeint ist.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.LinkExternalAsync(second, "sub-12345"));
    }

    [Fact]
    public async Task Die_Verknuepfung_laesst_sich_wieder_loesen()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        await users.LinkExternalAsync(userId, "sub-12345");
        await users.LinkExternalAsync(userId, null);

        Assert.Null(await users.AuthenticateExternalAsync("sub-12345"));
        Assert.Null((await users.GetUserAsync(userId))!.ExternalId);
    }

    [Fact]
    public async Task Ein_gesperrtes_Konto_kommt_auch_extern_nicht_herein()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        await users.LinkExternalAsync(userId, "sub-12345");

        // Ein zweiter Verwalter, damit die Sperre nicht am letzten Verwalter scheitert.
        await SeedUserAsync(test, "brida");
        await users.UpdateUserAsync(userId, "Alrik", isAdministrator: true, isDisabled: true);

        Assert.Null(await users.AuthenticateExternalAsync("sub-12345"));
    }
}
