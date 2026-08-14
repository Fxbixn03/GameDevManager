using GameDevManager.Data.Services;
using GameDevManager.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Berechtigungen je Benutzer: lesen oder auch schreiben, welche Module, Export/Import.
/// Durchgesetzt wird zentral — der <see cref="WriteGuardInterceptor"/> am SaveChanges und der
/// <see cref="PermissionGuard"/> an den Wegen daran vorbei. Geprüft wird deshalb über die
/// echten Modul-Dienste, nicht an den Wachen vorbei.
/// </summary>
public class PermissionTests
{
    /// <summary>Ein Konto, das alles darf außer schreiben — der „Leser“.</summary>
    private static readonly UserPermissions ReadOnly = new(
        IsAdministrator: false, CanWrite: false, CanExport: true, CanImport: true, AllowedModules: null);

    [Fact]
    public async Task Ein_Nur_Lese_Konto_kann_nichts_speichern()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        test.Permissions.Current = ReadOnly;

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";

        await Assert.ThrowsAsync<ContentValidationException>(() => items.SaveItemAsync(context));

        await using var db = test.CreateContext();
        Assert.False(await db.Items.AnyAsync());
    }

    [Fact]
    public async Task Ein_Nur_Lese_Konto_kann_nichts_loeschen()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        test.Permissions.Current = ReadOnly;

        // Der Löschpfad läuft über ExecuteDelete am Änderungsverfolger vorbei — abgefangen
        // wird er trotzdem, weil der Protokolleintrag davor durch den Interceptor muss.
        await Assert.ThrowsAsync<ContentValidationException>(() => items.DeleteItemAsync(context.Entity.Id));

        await using var db = test.CreateContext();
        Assert.True(await db.Items.AnyAsync(i => i.Id == context.Entity.Id));
    }

    [Fact]
    public async Task Das_eigene_Passwort_geht_auch_ohne_Schreibrecht()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var id = await users.CreateUserAsync("lena", "Lena", "Geheim1234", isAdministrator: false);

        // AppUser ist die eine Ausnahme des Schreibschutzes — sonst könnte ein Leser nicht
        // einmal sein Passwort wechseln.
        test.Permissions.Current = ReadOnly;
        await users.ChangeOwnPasswordAsync(id, "Geheim1234", "NeuGeheim1234");

        test.Permissions.Current = UserPermissions.Full;
        Assert.NotNull(await users.AuthenticateAsync("lena", "NeuGeheim1234"));
    }

    [Fact]
    public async Task Benutzer_verwalten_duerfen_nur_Verwalter()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        var id = await users.CreateUserAsync("admin", "Admin", "Geheim1234", isAdministrator: true);

        // Volles Schreibrecht, aber kein Verwalter — die Benutzerverwaltung bleibt zu.
        test.Permissions.Current = ReadOnly with { CanWrite = true };

        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.CreateUserAsync("neu", "Neu", "Geheim1234", isAdministrator: false));
        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.UpdateUserAsync(id, "Anders", isAdministrator: true, isDisabled: false));
        await Assert.ThrowsAsync<ContentValidationException>(
            () => users.SetPasswordAsync(id, "NeuGeheim1234"));
        await Assert.ThrowsAsync<ContentValidationException>(() => users.DeleteUserAsync(id));
    }

    [Fact]
    public async Task Berechtigungen_werden_gespeichert_und_bei_der_Anmeldung_mitgeliefert()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        await users.CreateUserAsync("admin", "Admin", "Geheim1234", isAdministrator: true);
        await users.CreateUserAsync(
            "leser", "Leser", "Geheim1234", isAdministrator: false,
            canWrite: false, canExport: false, canImport: true,
            allowedModuleKeys: "Items, npcs");

        var row = await users.AuthenticateAsync("leser", "Geheim1234");
        Assert.NotNull(row);

        var permissions = row.Permissions;
        Assert.False(permissions.CanWrite);
        Assert.False(permissions.CanExport);
        Assert.True(permissions.CanImport);
        Assert.True(permissions.CanAccessModule(ModuleKeys.Items));
        Assert.True(permissions.CanAccessModule(ModuleKeys.Npcs));
        Assert.False(permissions.CanAccessModule(ModuleKeys.Crafting));
    }

    [Fact]
    public async Task Verwalter_haben_immer_alle_Rechte()
    {
        using var test = new TestDatabase();
        var users = test.GetService<UserService>();

        // Selbst wenn Einschränkungen mitgegeben werden: Für Verwalter zählen sie nicht.
        await users.CreateUserAsync(
            "chef", "Chef", "Geheim1234", isAdministrator: true,
            canWrite: false, canExport: false, canImport: false, allowedModuleKeys: "items");

        var row = await users.AuthenticateAsync("chef", "Geheim1234");

        Assert.Equal(UserPermissions.Full, row!.Permissions);
    }

    [Fact]
    public void Die_Modulliste_liest_sich_robust_und_schreibt_sich_stabil()
    {
        Assert.Null(UserPermissions.ParseModuleKeys(null));
        Assert.Null(UserPermissions.ParseModuleKeys("   "));
        Assert.Null(UserPermissions.FormatModuleKeys(null));

        var parsed = UserPermissions.ParseModuleKeys(" Npcs, items ,,ITEMS ");
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Count);

        // Stabil sortiert und kleingeschrieben — derselbe Satz ergibt dieselbe Spalte.
        Assert.Equal("items,npcs", UserPermissions.FormatModuleKeys(parsed));
    }

    [Fact]
    public async Task Der_Import_braucht_Importrecht_und_Schreibrecht()
    {
        using var test = new TestDatabase();
        var import = test.GetService<ImportService>();

        test.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanImport = false };
        await Assert.ThrowsAsync<ContentValidationException>(
            () => import.ImportAsync(test.ProjectId, Stream.Null, replaceExisting: false));

        // Importrecht ohne Schreibrecht genügt nicht — ein Import schreibt den ganzen Bestand.
        test.Permissions.Current = ReadOnly;
        await Assert.ThrowsAsync<ContentValidationException>(
            () => import.ImportAsync(test.ProjectId, Stream.Null, replaceExisting: false));
    }

    [Fact]
    public async Task Exportstaende_brauchen_das_Exportrecht_das_Sicherheitsnetz_nicht()
    {
        using var test = new TestDatabase();
        var snapshots = test.GetService<ExportSnapshotService>();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        test.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanExport = false };

        await Assert.ThrowsAsync<ContentValidationException>(
            () => snapshots.CreateAsync(test.ProjectId, includeAssets: false));

        // Das Sicherheitsnetz gehört zum Import und zum Projektlöschen — es darf nicht am
        // fehlenden Exportrecht reißen.
        var safetyNet = await snapshots.CreateSafetyNetAsync(test.ProjectId);
        Assert.True(safetyNet.EntryCount > 0);
    }

    [Fact]
    public async Task Die_Suche_zeigt_nur_freigegebene_Module()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();
        var search = test.GetService<SearchService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";
        await items.SaveItemAsync(context);

        test.Permissions.Current = UserPermissions.Full with
        {
            IsAdministrator = false,
            AllowedModules = new HashSet<string> { ModuleKeys.Npcs }
        };
        Assert.Empty(await search.SearchAsync(test.ProjectId, "Eisen"));

        test.Permissions.Current = UserPermissions.Full;
        Assert.Single(await search.SearchAsync(test.ProjectId, "Eisen"));
    }

    [Fact]
    public async Task Projekt_duplizieren_braucht_nur_das_Schreibrecht()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();
        var projects = test.GetService<ProjectService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        // Export und Import sind beim Duplizieren nur interne Zwischenschritte — die
        // eigenen Rechte dafür braucht es nicht.
        test.Permissions.Current = UserPermissions.Full with
        {
            IsAdministrator = false, CanExport = false, CanImport = false
        };

        var copy = await projects.DuplicateProjectAsync(test.ProjectId, "Kopie", null);

        await using var db = test.CreateContext();
        Assert.Equal(1, await db.Items.CountAsync(i => i.GameProjectId == copy.Id));

        // Ohne Schreibrecht scheitert das Duplizieren dagegen sofort.
        test.Permissions.Current = ReadOnly;
        await Assert.ThrowsAsync<ContentValidationException>(
            () => projects.DuplicateProjectAsync(test.ProjectId, "Zweite Kopie", null));
    }
}
