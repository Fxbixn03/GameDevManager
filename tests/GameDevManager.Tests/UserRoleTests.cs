using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Rollen: gebündelte Rechte als Vorgabe je Konto. Aufgelöst wird an der einen Stelle
/// (<c>UserPermissions.For</c>) — die Rolle ist die Vorgabe, das Konto darf abweichen, und
/// Verwalter bekommen weiterhin alles.
/// </summary>
public class UserRoleTests
{
    /// <summary>Der erste Benutzer wird immer Verwalter — die Tests brauchen einen davor.</summary>
    private static Task SeedAdminAsync(UserService users) =>
        users.CreateUserAsync("admin", "Admin", "Geheim1234", isAdministrator: true);

    private static async Task<Guid> SeedRoleAsync(
        UserService users, string name, bool canWrite = true, bool canExport = true,
        bool canImport = true, string? allowedModuleKeys = null) =>
        await users.SaveRoleAsync(new UserRole
        {
            Name = name,
            CanWrite = canWrite,
            CanExport = canExport,
            CanImport = canImport,
            AllowedModuleKeys = allowedModuleKeys
        });

    [Fact]
    public async Task Die_Rolle_gibt_die_Rechte_vor()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        var roleId = await SeedRoleAsync(users, "Nur lesen", canWrite: false, allowedModuleKeys: "items");
        await users.CreateUserAsync("leser", "Leser", "Geheim1234", isAdministrator: false, roleId: roleId);

        var permissions = (await users.AuthenticateAsync("leser", "Geheim1234"))!.Permissions;

        Assert.False(permissions.CanWrite);
        Assert.True(permissions.CanAccessModule(ModuleKeys.Items));
        Assert.False(permissions.CanAccessModule(ModuleKeys.Crafting));
    }

    [Fact]
    public async Task Ein_abweichendes_Konto_schlaegt_die_Rolle()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        var roleId = await SeedRoleAsync(users, "Nur lesen", canWrite: false);
        await users.CreateUserAsync("autor", "Autor", "Geheim1234", isAdministrator: false,
            canWrite: true, roleId: roleId, overridesRole: true);

        Assert.True((await users.AuthenticateAsync("autor", "Geheim1234"))!.Permissions.CanWrite);
    }

    [Fact]
    public async Task Eine_geaenderte_Rolle_wirkt_ohne_das_Konto_anzufassen()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        var roleId = await SeedRoleAsync(users, "Autor");
        var userId = await users.CreateUserAsync(
            "autor", "Autor", "Geheim1234", isAdministrator: false, roleId: roleId);

        await users.SaveRoleAsync(new UserRole { Id = roleId, Name = "Autor", CanExport = false });

        // Live aufgelöst statt gestempelt — die Änderung gilt ab der nächsten Anmeldung.
        Assert.False((await users.GetUserAsync(userId))!.Permissions.CanExport);
    }

    [Fact]
    public async Task Eine_geloeschte_Rolle_stempelt_ihre_Rechte_auf_die_Konten()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        var roleId = await SeedRoleAsync(users, "Nur lesen", canWrite: false, allowedModuleKeys: "items");
        var userId = await users.CreateUserAsync(
            "leser", "Leser", "Geheim1234", isAdministrator: false, roleId: roleId);

        await users.DeleteRoleAsync(roleId);

        var row = (await users.GetUserAsync(userId))!;

        // Das Konto fällt nicht auf seine alten Spalten zurück — niemand bekommt still mehr.
        Assert.Null(row.Role);
        Assert.False(row.Permissions.CanWrite);
        Assert.False(row.Permissions.CanAccessModule(ModuleKeys.Crafting));
    }

    [Fact]
    public async Task Verwalter_bekommen_trotz_Rolle_alles()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        var roleId = await SeedRoleAsync(users, "Nur lesen", canWrite: false);
        await users.CreateUserAsync("chef", "Chef", "Geheim1234", isAdministrator: true, roleId: roleId);

        Assert.Equal(UserPermissions.Full, (await users.AuthenticateAsync("chef", "Geheim1234"))!.Permissions);
    }

    [Fact]
    public async Task Rollennamen_sind_eindeutig()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);
        await SeedRoleAsync(users, "Autor");

        await Assert.ThrowsAsync<ContentValidationException>(() => SeedRoleAsync(users, "  autor "));
    }

    [Fact]
    public async Task Eine_unbekannte_Rolle_wird_beim_Zuweisen_abgelehnt()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();
        await SeedAdminAsync(users);

        await Assert.ThrowsAsync<ContentValidationException>(() => users.CreateUserAsync(
            "leser", "Leser", "Geheim1234", isAdministrator: false, roleId: Guid.NewGuid()));
    }
}
