using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die schreibende API (F36). Der Schreibpfad führt durch dieselbe Strecke wie die Maske —
/// Pflichtfelder, Wertegrenzen, Schreibkonflikt-Erkennung, Schreibschutz und
/// Änderungsprotokoll greifen von selbst; genau das war die Anforderungsliste, unter der die
/// API bisher nur lesen durfte.
/// </summary>
public class ApiWriteTests
{
    private static async Task<FieldDefinition> SeedFieldAsync(TestDatabase test, bool required = false)
    {
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await types.SaveTypeAsync(type);

        var field = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = type.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer,
            IsRequired = required,
            MaxValue = 100
        };
        await types.SaveFieldAsync(field);

        // Die Art hängt am Feld — der Aufrufer braucht beide.
        field.ContentTypeId = type.Id;
        return field;
    }

    [Fact]
    public async Task Anlegen_und_Aendern_gehen_ueber_denselben_Weg()
    {
        using var test = new TestDatabase();
        var writer = test.GetService<ContentApiWriteService>();

        var created = await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Eisenschwert" });

        Assert.True(created.Created);

        var updated = await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items,
            new ContentWrite { Id = created.Id, Name = "Eisenschwert +1" });

        Assert.False(updated.Created);
        Assert.Equal(created.Id, updated.Id);

        await using var db = test.CreateContext();
        Assert.Equal("Eisenschwert +1", (await db.Items.SingleAsync()).Name);
    }

    [Fact]
    public async Task Feldwerte_kommen_mit_und_werden_geprueft()
    {
        using var test = new TestDatabase();
        var field = await SeedFieldAsync(test);
        var writer = test.GetService<ContentApiWriteService>();

        var write = new ContentWrite
        {
            Name = "Dolch",
            ContentTypeId = field.ContentTypeId,
            Values = { [field.Id] = new FieldValue
            {
                FieldDefinitionId = field.Id, OwnerModuleKey = ModuleKeys.Items, NumberValue = 12
            } }
        };

        var result = await writer.WriteAsync(test.ProjectId, ModuleKeys.Items, write);

        await using var db = test.CreateContext();
        var value = await db.FieldValues.SingleAsync(v => v.OwnerEntityId == result.Id);

        Assert.Equal(12, value.NumberValue);
    }

    [Fact]
    public async Task Die_Wertegrenze_gilt_wie_in_der_Maske()
    {
        using var test = new TestDatabase();
        var field = await SeedFieldAsync(test);
        var writer = test.GetService<ContentApiWriteService>();

        var write = new ContentWrite
        {
            Name = "Zu stark",
            ContentTypeId = field.ContentTypeId,
            Values = { [field.Id] = new FieldValue
            {
                FieldDefinitionId = field.Id, OwnerModuleKey = ModuleKeys.Items, NumberValue = 5000
            } }
        };

        // Sonst wäre die API der Weg, die Grenze zu umgehen.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => writer.WriteAsync(test.ProjectId, ModuleKeys.Items, write));
    }

    [Fact]
    public async Task Ein_Pflichtfeld_wird_verlangt()
    {
        using var test = new TestDatabase();
        var field = await SeedFieldAsync(test, required: true);
        var writer = test.GetService<ContentApiWriteService>();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => writer.WriteAsync(test.ProjectId, ModuleKeys.Items, new ContentWrite
            {
                Name = "Ohne Schaden",
                ContentTypeId = field.ContentTypeId
            }));
    }

    [Fact]
    public async Task Ein_ueberholter_Stand_meldet_einen_Schreibkonflikt()
    {
        using var test = new TestDatabase();
        var writer = test.GetService<ContentApiWriteService>();

        var created = await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Trank" });

        // Jemand anders war schneller.
        await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items, new ContentWrite { Id = created.Id, Name = "Trank (fremd)" });

        await Assert.ThrowsAsync<ContentConcurrencyException>(
            () => writer.WriteAsync(test.ProjectId, ModuleKeys.Items, new ContentWrite
            {
                Id = created.Id,
                Name = "Trank (meins)",
                ExpectedUpdatedAtUtc = created.UpdatedAtUtc
            }));
    }

    [Fact]
    public async Task Ohne_Namen_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<ContentApiWriteService>()
                .WriteAsync(test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "  " }));
    }

    [Fact]
    public async Task Ein_unbekanntes_Modul_wird_abgelehnt()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<ContentApiWriteService>()
                .WriteAsync(test.ProjectId, "gibtsnicht", new ContentWrite { Name = "X" }));
    }

    [Fact]
    public async Task Derselbe_Idempotenz_Schluessel_legt_nur_einmal_an()
    {
        using var test = new TestDatabase();
        var writer = test.GetService<ContentApiWriteService>();
        var key = Guid.NewGuid().ToString("N");

        var first = await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Fackel" }, key);

        var second = await writer.WriteAsync(
            test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Fackel" }, key);

        // Ein wiederholter Aufruf nach einem Verbindungsabbruch darf nichts doppelt anlegen.
        Assert.Equal(first.Id, second.Id);

        await using var db = test.CreateContext();
        Assert.Single(await db.Items.ToListAsync());
    }

    [Fact]
    public async Task Ein_Nur_Lese_Konto_kommt_nicht_durch()
    {
        using var test = new TestDatabase();

        // Der WriteGuardInterceptor greift am SaveChanges — der Schreibpfad der API läuft
        // durch dasselbe Speichern wie die Maske.
        test.Permissions.Current = new UserPermissions(
            IsAdministrator: false, CanWrite: false, CanExport: true, CanImport: true, AllowedModules: null);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => test.GetService<ContentApiWriteService>()
                .WriteAsync(test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Verboten" }));
    }

    [Fact]
    public async Task Der_Schreibvorgang_steht_im_Aenderungsprotokoll()
    {
        using var test = new TestDatabase();

        await test.GetService<ContentApiWriteService>()
            .WriteAsync(test.ProjectId, ModuleKeys.Items, new ContentWrite { Name = "Protokolliert" });

        await using var db = test.CreateContext();
        var entry = await db.ChangeLogEntries.SingleAsync();

        Assert.Equal(ModuleKeys.Items, entry.ModuleKey);
        Assert.Equal("Protokolliert", entry.EntityName);
        Assert.Equal(ChangeAction.Created, entry.Action);
    }
}
