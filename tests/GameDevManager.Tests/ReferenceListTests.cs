using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Der Feldtyp „Referenzliste“: mehrere Entitäten in einem Feld. Der interessante Teil ist
/// nicht das Speichern, sondern dass die Liste überall dort ankommt, wo eine einzelne Referenz
/// ankäme — in der Referenzansicht, im Export und beim Duplizieren.
/// </summary>
public class ReferenceListTests
{
    private static async Task<(ContentType Type, FieldDefinition Field)> SeedFieldAsync(TestDatabase test)
    {
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        await types.SaveTypeAsync(type);

        var field = new FieldDefinition
        {
            ContentTypeId = type.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Effekte",
            Type = ContentFieldType.EntityReference,
            ReferenceModuleKey = ModuleKeys.Effects,
            IsMultiValue = true
        };

        await types.SaveFieldAsync(field);

        return (type, field);
    }

    private static async Task<List<Guid>> SeedEffectsAsync(TestDatabase test, params string[] names)
    {
        await using var db = test.CreateContext();

        var effects = names
            .Select(name => new GameEffect { GameProjectId = test.ProjectId, Name = name })
            .ToList();

        db.GameEffects.AddRange(effects);
        await db.SaveChangesAsync();

        return [.. effects.Select(effect => effect.Id)];
    }

    private static async Task<Guid> SaveItemAsync(
        TestDatabase test, ContentType type, FieldDefinition field, string? rawValue)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Schwert";
        context.Entity.ContentTypeId = type.Id;
        context.ValueFor(context.ApplicableFields.Single(f => f.Id == field.Id)).TextValue = rawValue;

        await items.SaveItemAsync(context);
        return context.Entity.Id;
    }

    [Fact]
    public void Die_Liste_wird_kanonisch_geschrieben_und_gelesen()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var text = GuidList.Format([first, second, first]);

        // Ohne Dublette, mit „; “ verbunden — derselbe Stand ergibt denselben Export.
        Assert.Equal($"{first}; {second}", text);
        Assert.Equal(new[] { first, second }, GuidList.Parse(text));

        // Was keine GUID ist, fällt heraus: Ein Feld, das erst später zur Liste wurde, trägt
        // noch seinen alten Text und darf davon nicht umfallen.
        Assert.Empty(GuidList.Parse("irgendein Text"));
        Assert.Null(GuidList.Normalize("   "));
    }

    [Fact]
    public async Task Der_Wert_wird_kanonisiert_gespeichert()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedFieldAsync(test);
        var effects = await SeedEffectsAsync(test, "Feuer", "Eis");

        var itemId = await SaveItemAsync(test, type, field, $" {effects[1]} ;{effects[0]};{effects[1]} ");

        await using var db = test.CreateContext();
        var value = await db.FieldValues.SingleAsync(v => v.OwnerEntityId == itemId);

        Assert.Equal($"{effects[1]}; {effects[0]}", value.TextValue);

        // Die Einzelspalte bleibt leer — zwei Orte für dasselbe Feld liefen auseinander.
        Assert.Null(value.ReferenceValue);
    }

    [Fact]
    public async Task Die_Referenzansicht_findet_jedes_Ziel_der_Liste()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedFieldAsync(test);
        var effects = await SeedEffectsAsync(test, "Feuer", "Eis", "Gift");

        var itemId = await SaveItemAsync(test, type, field, GuidList.Format([effects[0], effects[1]]));

        var references = test.GetService<ReferenceService>();

        var hit = Assert.Single(await references.FindReferencesAsync(effects[0]));
        Assert.Equal(itemId, hit.SourceEntityId);
        Assert.Equal("Effekte", hit.FieldName);

        Assert.Single(await references.FindReferencesAsync(effects[1]));

        // Was nicht in der Liste steht, wird auch nicht gefunden.
        Assert.Empty(await references.FindReferencesAsync(effects[2]));
    }

    [Fact]
    public async Task Die_Liste_uebersteht_Export_und_Import()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedFieldAsync(test);
        var effects = await SeedEffectsAsync(test, "Feuer", "Eis");
        var expected = GuidList.Format([effects[0], effects[1]]);

        await SaveItemAsync(test, type, field, expected);

        using var zip = new MemoryStream();
        await test.GetService<ExportService>()
            .WriteExportAsync(test.ProjectId, ExportTarget.Json, includeAssets: false, zip);
        zip.Position = 0;

        await test.GetService<ImportService>().ImportAsync(test.ProjectId, zip, replaceExisting: true);

        await using var db = test.CreateContext();

        Assert.True(await db.FieldDefinitions.SingleAsync(f => f.Id == field.Id) is { IsMultiValue: true });
        Assert.Equal(expected, (await db.FieldValues.SingleAsync()).TextValue);
    }

    [Fact]
    public async Task Eine_Kopie_zeigt_auf_dieselben_fremden_Entitaeten()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedFieldAsync(test);
        var effects = await SeedEffectsAsync(test, "Feuer", "Eis");
        var expected = GuidList.Format([effects[0], effects[1]]);

        var itemId = await SaveItemAsync(test, type, field, expected);

        var copyId = await test.GetService<EntityDuplicationService>()
            .DuplicateAsync(test.ProjectId, ModuleKeys.Items, itemId);

        await using var db = test.CreateContext();
        var copied = await db.FieldValues.SingleAsync(v => v.OwnerEntityId == copyId);

        // Die Effekte werden nicht mitkopiert — die Kopie soll auf dieselben zeigen.
        Assert.Equal(expected, copied.TextValue);
    }

    [Fact]
    public async Task Der_Schalter_verschwindet_beim_Typwechsel()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();
        var (_, field) = await SeedFieldAsync(test);

        field.Type = ContentFieldType.Text;
        await types.SaveFieldAsync(field);

        var stored = (await types.GetTypesAsync(test.ProjectId, ModuleKeys.Items))
            .Single()
            .Fields
            .Single();

        Assert.False(stored.IsMultiValue);
    }
}
