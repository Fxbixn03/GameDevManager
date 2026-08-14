using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Massenbearbeitung: dieselbe Änderung an vielen Einträgen auf einmal. Geprüft wird vor
/// allem, was sie <b>nicht</b> tut — fremde Projekte anfassen, Werte an Arten schreiben, die
/// das Feld gar nicht führen, oder beim Artwechsel unsichtbaren Inhalt stehen lassen.
/// </summary>
public class BulkEditTests
{
    private sealed record Fixture(Guid TypeA, Guid TypeB, Guid FieldA, List<Guid> Items);

    /// <summary>
    /// Zwei Item-Arten, ein Feld an der ersten, drei Items dieser Art.
    /// </summary>
    private static async Task<Fixture> SeedAsync(TestDatabase database, Guid projectId)
    {
        await using var db = database.CreateContext();

        var typeA = new ContentType { GameProjectId = projectId, ModuleKey = ModuleKeys.Items, Name = "Waffe" };
        var typeB = new ContentType { GameProjectId = projectId, ModuleKey = ModuleKeys.Items, Name = "Trank" };

        var fieldA = new FieldDefinition
        {
            ContentTypeId = typeA.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };

        var items = new List<Item>
        {
            new() { GameProjectId = projectId, ContentTypeId = typeA.Id, Name = "Schwert" },
            new() { GameProjectId = projectId, ContentTypeId = typeA.Id, Name = "Axt" },
            new() { GameProjectId = projectId, ContentTypeId = typeA.Id, Name = "Bogen" }
        };

        db.ContentTypes.AddRange(typeA, typeB);
        db.FieldDefinitions.Add(fieldA);
        db.Items.AddRange(items);
        await db.SaveChangesAsync();

        return new Fixture(typeA.Id, typeB.Id, fieldA.Id, [.. items.Select(item => item.Id)]);
    }

    private static FieldValue Template(double number) => new() { OwnerModuleKey = string.Empty, NumberValue = number };

    [Fact]
    public async Task Ein_Feldwert_landet_bei_allen_markierten_Eintraegen()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var result = await database.GetService<BulkEditService>().SetFieldValueAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, seed.FieldA, Template(7));

        Assert.Equal(3, result.Changed);

        await using var db = database.CreateContext();
        var values = await db.FieldValues.Where(value => value.FieldDefinitionId == seed.FieldA).ToListAsync();

        Assert.Equal(3, values.Count);
        Assert.All(values, value => Assert.Equal(7, value.NumberValue));
    }

    [Fact]
    public async Task Ein_leerer_Wert_loescht_das_Feld_ueberall()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var bulk = database.GetService<BulkEditService>();
        await bulk.SetFieldValueAsync(database.ProjectId, ModuleKeys.Items, seed.Items, seed.FieldA, Template(7));

        var cleared = await bulk.SetFieldValueAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, seed.FieldA,
            new FieldValue { OwnerModuleKey = string.Empty });

        Assert.Equal(3, cleared.Changed);

        await using var db = database.CreateContext();
        Assert.Empty(await db.FieldValues.Where(value => value.FieldDefinitionId == seed.FieldA).ToListAsync());
    }

    [Fact]
    public async Task Eintraege_deren_Art_das_Feld_nicht_fuehrt_bleiben_unberuehrt()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        // Ein Trank führt das Feld „Schaden“ nicht — es hängt an der Art „Waffe“.
        await using (var db = database.CreateContext())
        {
            var potion = new Item
            {
                GameProjectId = database.ProjectId,
                ContentTypeId = seed.TypeB,
                Name = "Heiltrank"
            };
            db.Items.Add(potion);
            await db.SaveChangesAsync();
            seed.Items.Add(potion.Id);
        }

        var result = await database.GetService<BulkEditService>().SetFieldValueAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, seed.FieldA, Template(7));

        Assert.Equal(3, result.Changed);
        Assert.Equal(1, result.Skipped);

        await using var check = database.CreateContext();
        Assert.Equal(3, await check.FieldValues.CountAsync(value => value.FieldDefinitionId == seed.FieldA));
    }

    [Fact]
    public async Task Der_Artwechsel_nimmt_die_Werte_mit_die_nicht_mehr_gelten()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var bulk = database.GetService<BulkEditService>();
        await bulk.SetFieldValueAsync(database.ProjectId, ModuleKeys.Items, seed.Items, seed.FieldA, Template(7));

        var result = await bulk.AssignTypeAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, seed.TypeB);

        Assert.Equal(3, result.Changed);

        await using var db = database.CreateContext();
        Assert.All(
            await db.Items.Where(item => seed.Items.Contains(item.Id)).ToListAsync(),
            item => Assert.Equal(seed.TypeB, item.ContentTypeId));

        // Das Feld hängt an der alten Art — sein Wert wäre sonst unsichtbarer Inhalt, der im
        // Export und in der Referenzansicht wieder auftauchte.
        Assert.Empty(await db.FieldValues.Where(value => value.FieldDefinitionId == seed.FieldA).ToListAsync());
    }

    [Fact]
    public async Task Eine_Art_aus_einem_fremden_Projekt_wird_abgelehnt()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);
        var foreign = await SeedAsync(database, other.Id);

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<BulkEditService>().AssignTypeAsync(
                database.ProjectId, ModuleKeys.Items, seed.Items, foreign.TypeA));
    }

    [Fact]
    public async Task Eintraege_eines_fremden_Projekts_werden_nicht_mitgeaendert()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);
        var foreign = await SeedAsync(database, other.Id);

        // Untergeschobene GUIDs aus einem anderen Projekt: Die Abfrage grenzt selbst ein.
        var result = await database.GetService<BulkEditService>().AssignTypeAsync(
            database.ProjectId, ModuleKeys.Items, [.. seed.Items, .. foreign.Items], seed.TypeB);

        Assert.Equal(3, result.Changed);

        await using var db = database.CreateContext();
        Assert.All(
            await db.Items.Where(item => foreign.Items.Contains(item.Id)).ToListAsync(),
            item => Assert.Equal(foreign.TypeA, item.ContentTypeId));
    }

    [Fact]
    public async Task Tags_lassen_sich_vergeben_und_wieder_entziehen()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var tagId = Guid.NewGuid();
        await database.GetService<TagService>().SaveTagAsync(
            database.ProjectId, tagId, "Startausrüstung", null, null, []);

        var bulk = database.GetService<BulkEditService>();

        var assigned = await bulk.SetTagAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, tagId, assign: true);
        Assert.Equal(3, assigned.Changed);

        // Zweimal vergeben ist kein Vorgang — sonst stünde dasselbe Tag doppelt an der Entität.
        var again = await bulk.SetTagAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, tagId, assign: true);
        Assert.Equal(0, again.Changed);

        var removed = await bulk.SetTagAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, tagId, assign: false);
        Assert.Equal(3, removed.Changed);

        await using var db = database.CreateContext();
        Assert.Empty(await db.ContentTagAssignments.Where(a => a.ContentTagId == tagId).ToListAsync());
    }

    [Fact]
    public async Task Ohne_Schreibrecht_aendert_die_Massenbearbeitung_nichts()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanWrite = false };

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<BulkEditService>().AssignTypeAsync(
                database.ProjectId, ModuleKeys.Items, seed.Items, seed.TypeB));

        database.Permissions.Current = UserPermissions.Full;

        await using var db = database.CreateContext();
        Assert.All(
            await db.Items.Where(item => seed.Items.Contains(item.Id)).ToListAsync(),
            item => Assert.Equal(seed.TypeA, item.ContentTypeId));
    }

    [Fact]
    public async Task Jede_geaenderte_Entitaet_steht_einzeln_im_Protokoll()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database, database.ProjectId);

        var before = await database.GetService<ChangeLogService>().GetEntriesAsync(database.ProjectId);

        await database.GetService<BulkEditService>().AssignTypeAsync(
            database.ProjectId, ModuleKeys.Items, seed.Items, seed.TypeB);

        var after = await database.GetService<ChangeLogService>().GetEntriesAsync(database.ProjectId);

        // Anders als beim Import ist hier jede Zeile eine bewusste Auswahl — und genau das
        // sucht man später im Protokoll.
        Assert.Equal(before.Total + 3, after.Total);
    }
}
