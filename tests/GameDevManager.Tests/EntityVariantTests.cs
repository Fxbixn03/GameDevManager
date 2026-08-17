using System.IO.Compression;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Varianten (F5): „Eisenschwert +1“ übernimmt jeden Feldwert des „Eisenschwerts“, den es
/// nicht selbst setzt. Dieselbe Idee wie bei den Unterarten, nur eine Ebene tiefer — dort erbt
/// eine Art die <b>Felder</b> einer anderen, hier erbt eine Entität die <b>Werte</b>.
/// </summary>
public class EntityVariantTests
{
    private static async Task<(ContentType Type, FieldDefinition Damage, FieldDefinition Weight)>
        SeedTypeAsync(TestDatabase test)
    {
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        await types.SaveTypeAsync(type);

        var damage = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = type.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };

        var weight = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = type.Id,
            Name = "Gewicht",
            Type = ContentFieldType.Decimal
        };

        await types.SaveFieldAsync(damage);
        await types.SaveFieldAsync(weight);

        return (type, damage, weight);
    }

    private static async Task<Item> SaveItemAsync(
        TestDatabase test, string name, Guid typeId, Guid? basedOnId = null,
        params (FieldDefinition Field, double Value)[] values)
    {
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null)
            ?? throw new InvalidOperationException();

        context.Entity.Name = name;
        context.Entity.ContentTypeId = typeId;
        context.Entity.BasedOnId = basedOnId;

        foreach (var (field, value) in values)
        {
            context.ValueFor(field).NumberValue = value;
        }

        await items.SaveItemAsync(context);
        return context.Entity;
    }

    private static async Task<Dictionary<Guid, FieldValue>> LoadValuesAsync(TestDatabase test, Guid itemId)
    {
        var context = await test.GetService<ItemService>().LoadForEditAsync(test.ProjectId, itemId);
        return context!.Values;
    }

    [Fact]
    public async Task Eine_Variante_erbt_was_sie_nicht_selbst_setzt()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var basis = await SaveItemAsync(test, "Eisenschwert", type.Id, null, (damage, 10), (weight, 3.5));
        var variant = await SaveItemAsync(test, "Eisenschwert +1", type.Id, basis.Id, (damage, 14));

        var values = await LoadValuesAsync(test, variant.Id);

        // Der eigene Wert gewinnt …
        Assert.Equal(14, values[damage.Id].NumberValue);
        Assert.False(values[damage.Id].IsInherited);

        // … das Übrige kommt vom Vorbild und ist als geerbt gekennzeichnet.
        Assert.Equal(3.5, values[weight.Id].NumberValue);
        Assert.True(values[weight.Id].IsInherited);
        Assert.Equal(basis.Id, values[weight.Id].InheritedFromEntityId);
    }

    [Fact]
    public async Task Geerbte_Werte_werden_nicht_als_eigene_Zeilen_gespeichert()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var basis = await SaveItemAsync(test, "Eisenschwert", type.Id, null, (damage, 10), (weight, 3.5));
        var variant = await SaveItemAsync(test, "Eisenschwert +1", type.Id, basis.Id, (damage, 14));

        // Zweimal speichern — der geerbte Wert stand nach dem Laden im Kontext und darf dabei
        // nicht zur eigenen Zeile werden: Das materialisierte die Vererbung und löste sie auf.
        var context = await test.GetService<ItemService>().LoadForEditAsync(test.ProjectId, variant.Id);
        await test.GetService<ItemService>().SaveItemAsync(context!);

        await using var db = test.CreateContext();
        var stored = await db.FieldValues.Where(v => v.OwnerEntityId == variant.Id).ToListAsync();

        Assert.Equal(damage.Id, Assert.Single(stored).FieldDefinitionId);
    }

    [Fact]
    public async Task Die_Kette_geht_ueber_mehrere_Stufen_der_naehere_Wert_gewinnt()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var grand = await SaveItemAsync(test, "Schwert", type.Id, null, (damage, 5), (weight, 4));
        var middle = await SaveItemAsync(test, "Eisenschwert", type.Id, grand.Id, (damage, 10));
        var leaf = await SaveItemAsync(test, "Eisenschwert +1", type.Id, middle.Id);

        var values = await LoadValuesAsync(test, leaf.Id);

        // Der Schaden kommt vom direkten Vorbild, nicht vom Großelternteil — man überschreibt
        // nach unten, nicht nach oben.
        Assert.Equal(10, values[damage.Id].NumberValue);
        Assert.Equal(middle.Id, values[damage.Id].InheritedFromEntityId);

        // Das Gewicht setzt nur die oberste Stufe.
        Assert.Equal(4, values[weight.Id].NumberValue);
        Assert.Equal(grand.Id, values[weight.Id].InheritedFromEntityId);
    }

    [Fact]
    public async Task Ein_Ring_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var (type, damage, _) = await SeedTypeAsync(test);

        var first = await SaveItemAsync(test, "A", type.Id, null, (damage, 1));
        var second = await SaveItemAsync(test, "B", type.Id, first.Id);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, first.Id);
        context!.Entity.BasedOnId = second.Id;

        await Assert.ThrowsAsync<ContentValidationException>(() => items.SaveItemAsync(context));
    }

    [Fact]
    public async Task Eine_Entitaet_kann_keine_Variante_ihrer_selbst_sein()
    {
        using var test = new TestDatabase();
        var (type, _, _) = await SeedTypeAsync(test);

        var item = await SaveItemAsync(test, "A", type.Id);

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, item.Id);
        context!.Entity.BasedOnId = item.Id;

        await Assert.ThrowsAsync<ContentValidationException>(() => items.SaveItemAsync(context));
    }

    [Fact]
    public async Task Ein_Vorbild_aus_einem_fremden_Modul_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var (type, _, _) = await SeedTypeAsync(test);
        var item = await SaveItemAsync(test, "A", type.Id);

        var npc = new Npc { GameProjectId = test.ProjectId, Name = "Alrik" };

        await using (var db = test.CreateContext())
        {
            db.Npcs.Add(npc);
            await db.SaveChangesAsync();
        }

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, item.Id);
        context!.Entity.BasedOnId = npc.Id;

        // Jedes Modul hat seine eigene Tabelle — ein NPC ist dort gar nicht zu finden.
        await Assert.ThrowsAsync<ContentValidationException>(() => items.SaveItemAsync(context));
    }

    [Fact]
    public async Task Beim_Loeschen_des_Vorbilds_behaelt_die_Variante_ihre_Werte()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var basis = await SaveItemAsync(test, "Eisenschwert", type.Id, null, (damage, 10), (weight, 3.5));
        var variant = await SaveItemAsync(test, "Eisenschwert +1", type.Id, basis.Id, (damage, 14));

        await test.GetService<ItemService>().DeleteItemAsync(basis.Id);

        var values = await LoadValuesAsync(test, variant.Id);

        // Der eigene Wert bleibt, der geerbte ist jetzt ihrer — nichts geht verloren.
        Assert.Equal(14, values[damage.Id].NumberValue);
        Assert.Equal(3.5, values[weight.Id].NumberValue);
        Assert.False(values[weight.Id].IsInherited);

        await using var db = test.CreateContext();
        var stored = await db.Items.SingleAsync(item => item.Id == variant.Id);
        Assert.Null(stored.BasedOnId);
    }

    [Fact]
    public async Task Beim_Loeschen_einer_Zwischenstufe_rueckt_die_Kette_nach()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var grand = await SaveItemAsync(test, "Schwert", type.Id, null, (weight, 4));
        var middle = await SaveItemAsync(test, "Eisenschwert", type.Id, grand.Id, (damage, 10));
        var leaf = await SaveItemAsync(test, "Eisenschwert +1", type.Id, middle.Id);

        await test.GetService<ItemService>().DeleteItemAsync(middle.Id);

        await using (var db = test.CreateContext())
        {
            var stored = await db.Items.SingleAsync(item => item.Id == leaf.Id);
            Assert.Equal(grand.Id, stored.BasedOnId);
        }

        var values = await LoadValuesAsync(test, leaf.Id);

        // Der Wert der Zwischenstufe ist jetzt eigener, der des Großelternteils bleibt geerbt.
        Assert.Equal(10, values[damage.Id].NumberValue);
        Assert.False(values[damage.Id].IsInherited);
        Assert.Equal(4, values[weight.Id].NumberValue);
        Assert.Equal(grand.Id, values[weight.Id].InheritedFromEntityId);
    }

    [Fact]
    public async Task Der_Export_schreibt_aufgeloeste_Werte_mit_Herkunft()
    {
        using var test = new TestDatabase();
        var (type, damage, weight) = await SeedTypeAsync(test);

        var basis = await SaveItemAsync(test, "Eisenschwert", type.Id, null, (damage, 10), (weight, 3.5));
        var variant = await SaveItemAsync(test, "Eisenschwert +1", type.Id, basis.Id, (damage, 14));

        using var zip = new MemoryStream();
        await test.GetService<ExportService>().WriteExportAsync(
            test.ProjectId, ExportTarget.Json, includeAssets: false, zip);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        await using var content = archive.GetEntry("content/field-values.json")!.Open();
        using var document = await JsonDocument.ParseAsync(content);

        var values = document.RootElement.GetProperty("values").EnumerateArray()
            .Where(value => value.GetProperty("ownerEntityId").GetGuid() == variant.Id)
            .ToList();

        // Die Engine soll die Kette nicht selbst auflösen — beide Werte stehen da.
        Assert.Equal(2, values.Count);

        var own = values.Single(v => v.GetProperty("fieldDefinitionId").GetGuid() == damage.Id);
        Assert.Equal(14, own.GetProperty("numberValue").GetDouble());
        Assert.False(own.TryGetProperty("inheritedFromEntityId", out var none) && none.ValueKind != JsonValueKind.Null);

        var inherited = values.Single(v => v.GetProperty("fieldDefinitionId").GetGuid() == weight.Id);
        Assert.Equal(3.5, inherited.GetProperty("numberValue").GetDouble());
        Assert.Equal(basis.Id, inherited.GetProperty("inheritedFromEntityId").GetGuid());
    }

    [Fact]
    public async Task Ein_zurueckgenommener_Wert_wird_wieder_geerbt()
    {
        using var test = new TestDatabase();
        var (type, damage, _) = await SeedTypeAsync(test);

        var basis = await SaveItemAsync(test, "Eisenschwert", type.Id, null, (damage, 10));
        var variant = await SaveItemAsync(test, "Eisenschwert +1", type.Id, basis.Id, (damage, 14));

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, variant.Id);

        // „Wieder erben“ ist schlicht: leeren. Ein leerer Wert hinterlässt keine Zeile.
        context!.ValueFor(damage).Clear();
        await items.SaveItemAsync(context);

        var values = await LoadValuesAsync(test, variant.Id);

        Assert.Equal(10, values[damage.Id].NumberValue);
        Assert.True(values[damage.Id].IsInherited);
    }
}
