using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// CSV je Modul — der Weg Tabelle ↔ Tool. Geprüft werden der Zeichensalat-Teil (Anführungszeichen,
/// Umbrüche, Trennzeichen) und die Regel des Imports: aktualisieren über GUID oder Name, nie
/// löschen, was in keiner Zeile steht.
/// </summary>
public class CsvTests
{
    private sealed record Fixture(Guid TypeId, Guid DamageField, Guid ItemId);

    private static async Task<Fixture> SeedAsync(TestDatabase database)
    {
        await using var db = database.CreateContext();

        var type = new ContentType
        {
            GameProjectId = database.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        var damage = new FieldDefinition
        {
            ContentTypeId = type.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };

        var item = new Item
        {
            GameProjectId = database.ProjectId,
            ContentTypeId = type.Id,
            Name = "Schwert",
            Description = "Scharf."
        };

        db.ContentTypes.Add(type);
        db.FieldDefinitions.Add(damage);
        db.Items.Add(item);
        db.FieldValues.Add(new FieldValue
        {
            OwnerEntityId = item.Id,
            OwnerModuleKey = ModuleKeys.Items,
            FieldDefinitionId = damage.Id,
            NumberValue = 12
        });

        await db.SaveChangesAsync();

        return new Fixture(type.Id, damage.Id, item.Id);
    }

    // ------------------------------------------------------------------------ Das Format

    [Fact]
    public void Anfuehrungszeichen_Trennzeichen_und_Umbrueche_ueberstehen_den_Weg()
    {
        var original = new[] { "a;b", "sagt \"hallo\"", "Zeile1\nZeile2", " Rand ", "" };

        var line = Csv.FormatRow(original);
        var parsed = Csv.Parse(line, Csv.Separator);

        Assert.Equal(original, Assert.Single(parsed));
    }

    [Theory]
    [InlineData("id;name;art", ';')]
    [InlineData("id,name,art", ',')]
    // Ein Semikolon im Text darf die Erkennung nicht kippen — gezählt wird außerhalb der
    // Anführungszeichen.
    [InlineData("id,name,\"a;b;c;d\"", ',')]
    public void Das_Trennzeichen_wird_aus_der_Kopfzeile_erkannt(string header, char expected) =>
        Assert.Equal(expected, Csv.DetectSeparator(header));

    // ------------------------------------------------------------------------- Der Export

    [Fact]
    public async Task Der_Export_traegt_Stammdaten_Art_und_Felder()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var csv = await database.GetService<CsvContentService>().ExportAsync(database.ProjectId, ModuleKeys.Items);
        var rows = Csv.Parse(csv, Csv.Separator);

        Assert.Equal(["id", "name", "beschreibung", "art", "Schaden"], rows[0]);

        var row = Assert.Single(rows.Skip(1));
        Assert.Equal(seed.ItemId.ToString(), row[0]);
        Assert.Equal("Schwert", row[1]);
        Assert.Equal("Scharf.", row[2]);
        Assert.Equal("Waffe", row[3]);
        Assert.Equal("12", row[4]);
    }

    // ------------------------------------------------------------------------- Der Import

    [Fact]
    public async Task Der_Import_aktualisiert_ueber_die_GUID_Spalte()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var csv = $"id;name;beschreibung;art;Schaden\n{seed.ItemId};Langschwert;Sehr scharf.;Waffe;25\n";

        var result = await database.GetService<CsvContentService>()
            .ImportAsync(database.ProjectId, ModuleKeys.Items, csv, createMissing: true);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);

        await using var db = database.CreateContext();
        var item = await db.Items.FirstAsync(i => i.Id == seed.ItemId);

        Assert.Equal("Langschwert", item.Name);
        Assert.Equal("Sehr scharf.", item.Description);
        Assert.Equal(25, (await db.FieldValues.FirstAsync(v => v.OwnerEntityId == seed.ItemId)).NumberValue);
    }

    [Fact]
    public async Task Ohne_GUID_findet_der_Import_die_Zeile_ueber_den_Namen()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var result = await database.GetService<CsvContentService>().ImportAsync(
            database.ProjectId, ModuleKeys.Items, "name;Schaden\nSchwert;30\n", createMissing: true);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);

        await using var db = database.CreateContext();
        Assert.Equal(30, (await db.FieldValues.FirstAsync(v => v.OwnerEntityId == seed.ItemId)).NumberValue);
    }

    [Fact]
    public async Task Unbekannte_Zeilen_werden_angelegt_oder_uebersprungen()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        var csv = "name;art;Schaden\nAxt;Waffe;18\n";
        var service = database.GetService<CsvContentService>();

        var skipped = await service.ImportAsync(database.ProjectId, ModuleKeys.Items, csv, createMissing: false);
        Assert.Equal(0, skipped.Created);
        Assert.Single(skipped.Warnings);

        var created = await service.ImportAsync(database.ProjectId, ModuleKeys.Items, csv, createMissing: true);
        Assert.Equal(1, created.Created);

        await using var db = database.CreateContext();
        var axe = await db.Items.FirstAsync(item => item.Name == "Axt");
        Assert.Equal(18, (await db.FieldValues.FirstAsync(v => v.OwnerEntityId == axe.Id)).NumberValue);
    }

    [Fact]
    public async Task Was_in_keiner_Zeile_steht_bleibt_unangetastet()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        await using (var db = database.CreateContext())
        {
            db.Items.Add(new Item
            {
                GameProjectId = database.ProjectId,
                ContentTypeId = seed.TypeId,
                Name = "Bogen"
            });
            await db.SaveChangesAsync();
        }

        // Ein CSV ist ein Ausschnitt — es darf nichts löschen, was es nicht erwähnt.
        await database.GetService<CsvContentService>().ImportAsync(
            database.ProjectId, ModuleKeys.Items, "name;Schaden\nSchwert;99\n", createMissing: false);

        await using var check = database.CreateContext();
        Assert.Equal(2, await check.Items.CountAsync(item => item.GameProjectId == database.ProjectId));
    }

    [Fact]
    public async Task Eine_leere_Zelle_loescht_den_Feldwert()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        await database.GetService<CsvContentService>().ImportAsync(
            database.ProjectId, ModuleKeys.Items, $"id;Schaden\n{seed.ItemId};\n", createMissing: false);

        await using var db = database.CreateContext();
        Assert.Empty(await db.FieldValues.Where(v => v.OwnerEntityId == seed.ItemId).ToListAsync());
    }

    [Fact]
    public async Task Eine_unlesbare_Zelle_meldet_sich_und_laesst_die_Zeile_stehen()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var result = await database.GetService<CsvContentService>().ImportAsync(
            database.ProjectId, ModuleKeys.Items,
            $"id;name;Schaden\n{seed.ItemId};Langschwert;viel\n", createMissing: false);

        Assert.Single(result.Warnings);
        Assert.Equal(1, result.Updated);

        await using var db = database.CreateContext();
        var item = await db.Items.FirstAsync(i => i.Id == seed.ItemId);

        // Der Name der Zeile ist angekommen, nur die kaputte Zelle nicht.
        Assert.Equal("Langschwert", item.Name);
        Assert.Equal(12, (await db.FieldValues.FirstAsync(v => v.OwnerEntityId == seed.ItemId)).NumberValue);
    }

    [Fact]
    public async Task Eine_unveraenderte_Zeile_zaehlt_nicht_als_Aenderung()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        var service = database.GetService<CsvContentService>();
        var csv = await service.ExportAsync(database.ProjectId, ModuleKeys.Items);

        // Derselbe Stand wieder eingelesen: Ohne diese Prüfung bekäme das Änderungsprotokoll
        // bei jedem Import den ganzen Bestand.
        var result = await service.ImportAsync(database.ProjectId, ModuleKeys.Items, csv, createMissing: false);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Unchanged);
        Assert.Equal(seed.ItemId, (await database.CreateContext().Items.FirstAsync()).Id);
    }

    [Fact]
    public async Task Ohne_Schluesselspalte_wird_die_Datei_abgelehnt()
    {
        using var database = new TestDatabase();
        await SeedAsync(database);

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<CsvContentService>().ImportAsync(
                database.ProjectId, ModuleKeys.Items, "art;Schaden\nWaffe;10\n", createMissing: true));
    }

    [Fact]
    public async Task Der_Import_braucht_das_Importrecht()
    {
        using var database = new TestDatabase();
        var seed = await SeedAsync(database);

        database.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanImport = false };

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            database.GetService<CsvContentService>().ImportAsync(
                database.ProjectId, ModuleKeys.Items, $"id;name\n{seed.ItemId};Neu\n", createMissing: false));
    }
}
