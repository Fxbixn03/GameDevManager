using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Zwei-Faktor-Anmeldung (F47): TOTP nach RFC 6238, selbst gerechnet — dieselbe Abwägung wie
/// beim <c>ImageDimensionReader</c>, dem <c>Csv</c> und dem <c>CurveExpression</c>.
/// </summary>
public class TwoFactorTests
{
    private static async Task<Guid> SeedUserAsync(TestDatabase test, string password = "Geheim!1")
    {
        var users = test.GetService<UserService>();

        return await users.CreateUserAsync("alrik", "Alrik", password, isAdministrator: true);
    }

    // ------------------------------------------------------------------ Der Rechner

    [Fact]
    public void Ein_frisches_Geheimnis_ist_lesbares_Base32()
    {
        var secret = Totp.CreateSecret();

        Assert.Equal(32, secret.Length);
        Assert.All(secret, character => Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
    }

    [Fact]
    public void Der_eigene_Code_gilt()
    {
        var secret = Totp.CreateSecret();
        var now = DateTimeOffset.UtcNow;

        Assert.True(Totp.Verify(secret, Totp.Compute(secret, now), now));
    }

    [Fact]
    public void Der_Nachbarschritt_gilt_der_uebernaechste_nicht()
    {
        var secret = Totp.CreateSecret();
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        // Eine Uhr, die eine halbe Minute abweicht, soll niemanden aussperren …
        Assert.True(Totp.Verify(secret, Totp.Compute(secret, now.AddSeconds(-30)), now));
        Assert.True(Totp.Verify(secret, Totp.Compute(secret, now.AddSeconds(30)), now));

        // … das Zeitfenster darüber hinaus zu öffnen wäre aber keine Zurückhaltung mehr.
        Assert.False(Totp.Verify(secret, Totp.Compute(secret, now.AddSeconds(90)), now));
    }

    [Fact]
    public void Ein_falscher_Code_gilt_nicht()
    {
        var secret = Totp.CreateSecret();

        Assert.False(Totp.Verify(secret, "000000"));
        Assert.False(Totp.Verify(secret, "12345"));
        Assert.False(Totp.Verify(secret, null));
        Assert.False(Totp.Verify(null, "123456"));
    }

    [Fact]
    public void Die_otpauth_Adresse_traegt_Geheimnis_und_Konto()
    {
        var uri = Totp.BuildUri("GameDevManager", "alrik", "ABCDEFGH");

        Assert.StartsWith("otpauth://totp/GameDevManager:alrik?", uri);
        Assert.Contains("secret=ABCDEFGH", uri);
        Assert.Contains("period=30", uri);
    }

    // ------------------------------------------------------------------- Der Ablauf

    [Fact]
    public async Task Einrichten_gilt_erst_nach_der_Bestaetigung()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        var (secret, uri) = await users.StartTwoFactorAsync(userId);

        Assert.NotEmpty(secret);
        Assert.Contains("otpauth://", uri);

        // Noch nicht bestätigt: Wer das Geheimnis erzeugt, aber nie in seine App übernimmt,
        // soll sich nicht aussperren.
        Assert.False((await users.GetUserAsync(userId))!.HasTwoFactor);

        var codes = await users.ConfirmTwoFactorAsync(userId, Totp.Compute(secret, DateTimeOffset.UtcNow));

        Assert.Equal(10, codes.Count);
        Assert.True((await users.GetUserAsync(userId))!.HasTwoFactor);
    }

    [Fact]
    public async Task Ein_falscher_Code_bestaetigt_nicht()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        await users.StartTwoFactorAsync(userId);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.ConfirmTwoFactorAsync(userId, "000000"));
    }

    [Fact]
    public async Task Die_Anmeldung_verlangt_den_Code()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        var (secret, _) = await users.StartTwoFactorAsync(userId);
        await users.ConfirmTwoFactorAsync(userId, Totp.Compute(secret, DateTimeOffset.UtcNow));

        // Der erste Schritt sagt nur, dass das Passwort stimmt — und dass ein Code fehlt.
        var candidate = await users.VerifyPasswordAsync("alrik", "Geheim!1");
        Assert.NotNull(candidate);
        Assert.True(candidate.HasTwoFactor);

        // Ohne Code kein Zutritt.
        Assert.Null(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", "000000"));

        // Mit Code schon.
        var code = Totp.Compute(secret, DateTimeOffset.UtcNow);
        Assert.NotNull(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", code));
    }

    [Fact]
    public async Task Ein_Wiederherstellungscode_gilt_genau_einmal()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        var (secret, _) = await users.StartTwoFactorAsync(userId);
        var codes = await users.ConfirmTwoFactorAsync(userId, Totp.Compute(secret, DateTimeOffset.UtcNow));

        Assert.NotNull(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", codes[0]));

        // Ein Code, der zweimal gilt, ist keiner.
        Assert.Null(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", codes[0]));

        // Die übrigen gelten weiter.
        Assert.NotNull(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", codes[1]));
    }

    [Fact]
    public async Task Abschalten_verlangt_einen_gueltigen_Code()
    {
        using var test = new TestDatabase();
        var userId = await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        var (secret, _) = await users.StartTwoFactorAsync(userId);
        await users.ConfirmTwoFactorAsync(userId, Totp.Compute(secret, DateTimeOffset.UtcNow));

        // Sonst genügte ein übernommener Browser-Tab, um den zweiten Faktor loszuwerden.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.DisableTwoFactorAsync(userId, "000000"));

        await users.DisableTwoFactorAsync(userId, Totp.Compute(secret, DateTimeOffset.UtcNow));

        Assert.False((await users.GetUserAsync(userId))!.HasTwoFactor);

        await using var db = test.CreateContext();
        var stored = await db.AppUsers.SingleAsync(user => user.Id == userId);

        Assert.Null(stored.TotpSecret);
        Assert.Null(stored.TotpRecoveryCodes);
    }

    [Fact]
    public async Task Ohne_zweiten_Faktor_bleibt_die_Anmeldung_wie_zuvor()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var users = test.GetService<UserService>();

        Assert.NotNull(await users.AuthenticateAsync("alrik", "Geheim!1"));
        Assert.NotNull(await users.AuthenticateWithCodeAsync("alrik", "Geheim!1", string.Empty));
        Assert.Null(await users.AuthenticateWithCodeAsync("alrik", "Falsch!1", string.Empty));
    }
}
