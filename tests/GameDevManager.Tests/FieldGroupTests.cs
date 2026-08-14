using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Feldgruppen: der benannte Abschnitt, in dem ein Feld in der Bearbeitungsmaske steht. Eine
/// Textspalte am Feld — geprüft wird deshalb, dass sie durch Speichern, Vererbung, Kopieren
/// und Export unverändert hindurchgeht.
/// </summary>
public class FieldGroupTests
{
    private static async Task<ContentType> SeedTypeAsync(TestDatabase test, string name = "Waffe")
    {
        var type = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = name
        };

        await test.GetService<ContentTypeService>().SaveTypeAsync(type);
        return type;
    }

    private static Task SaveFieldAsync(
        TestDatabase test, Guid typeId, string name, string? group, int sortOrder = 0) =>
        test.GetService<ContentTypeService>().SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = typeId,
            ModuleKey = ModuleKeys.Items,
            Name = name,
            Type = ContentFieldType.Integer,
            GroupName = group,
            SortOrder = sortOrder
        });

    [Fact]
    public async Task Der_Abschnitt_wird_gespeichert_und_leer_normalisiert()
    {
        using var test = new TestDatabase();
        var type = await SeedTypeAsync(test);

        await SaveFieldAsync(test, type.Id, "Schaden", "  Kampfwerte  ");
        await SaveFieldAsync(test, type.Id, "Gewicht", "   ", sortOrder: 1);

        var fields = (await test.GetService<ContentTypeService>()
                .GetTypesAsync(test.ProjectId, ModuleKeys.Items))
            .Single()
            .Fields;

        Assert.Equal("Kampfwerte", fields.Single(f => f.Name == "Schaden").GroupName);

        // Nur Leerzeichen ist kein Abschnitt — sonst entstünde eine namenlose Überschrift.
        Assert.Null(fields.Single(f => f.Name == "Gewicht").GroupName);
    }

    [Fact]
    public async Task Ein_geerbtes_Feld_bringt_seinen_Abschnitt_mit()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedTypeAsync(test);
        await SaveFieldAsync(test, parent.Id, "Schaden", "Kampfwerte");

        var child = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Nahkampf",
            ParentId = parent.Id
        };

        await types.SaveTypeAsync(child);

        var stored = (await types.GetTypesAsync(test.ProjectId, ModuleKeys.Items))
            .Single(t => t.Id == child.Id);

        // Der Abschnitt hängt am Feld, nicht an der Art — die Unterart zeigt ihn deshalb
        // ohne Zutun unter derselben Überschrift.
        Assert.Equal("Kampfwerte", Assert.Single(stored.InheritedFields).GroupName);
    }

    [Fact]
    public async Task Der_Abschnitt_uebersteht_Export_und_Import()
    {
        using var test = new TestDatabase();
        var type = await SeedTypeAsync(test);

        await SaveFieldAsync(test, type.Id, "Schaden", "Kampfwerte");

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        await using var db = test.CreateContext();
        var field = await db.FieldDefinitions.SingleAsync();

        Assert.Equal("Kampfwerte", field.GroupName);
    }

    [Fact]
    public void Die_Kopie_einer_Felddefinition_traegt_den_Abschnitt_mit()
    {
        var original = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            GroupName = "Kampfwerte"
        };

        Assert.Equal("Kampfwerte", original.Clone().GroupName);
    }
}
