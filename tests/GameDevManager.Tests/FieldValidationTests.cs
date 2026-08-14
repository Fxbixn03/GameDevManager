using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Wertebereiche und Formatprüfung an Feldern: Grenzen an Zahlen, ein Muster an Texten.
/// Geprüft wird beim Speichern einer Entität — und beim Anlegen des Feldes, dass die Vorgabe
/// selbst Sinn ergibt.
/// </summary>
public class FieldValidationTests
{
    private static async Task<(ContentType Type, FieldDefinition Field)> SeedAsync(
        TestDatabase test, Action<FieldDefinition> configure, ContentFieldType type = ContentFieldType.Integer)
    {
        var types = test.GetService<ContentTypeService>();

        var contentType = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        await types.SaveTypeAsync(contentType);

        var field = new FieldDefinition
        {
            ContentTypeId = contentType.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            Type = type
        };

        configure(field);
        await types.SaveFieldAsync(field);

        return (contentType, field);
    }

    /// <summary>Legt ein Item der Art an und setzt den Wert des Feldes.</summary>
    private static async Task SaveItemAsync(
        TestDatabase test, ContentType type, FieldDefinition field, Action<FieldValue> setValue)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = "Schwert";

        // Die geltenden Felder hängen an der Art — die Maske hat sie beim Laden schon dabei.
        context.Entity.ContentTypeId = type.Id;

        setValue(context.ValueFor(context.ApplicableFields.Single(f => f.Id == field.Id)));

        await items.SaveItemAsync(context);
    }

    [Fact]
    public async Task Ein_Wert_unter_dem_Mindestwert_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(test, f => f.MinValue = 5);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemAsync(test, type, field, value => value.NumberValue = 4));

        // Genau auf der Grenze ist erlaubt — „mindestens 5“ schließt die 5 ein.
        await SaveItemAsync(test, type, field, value => value.NumberValue = 5);
    }

    [Fact]
    public async Task Ein_Wert_ueber_dem_Hoechstwert_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(test, f => f.MaxValue = 100);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemAsync(test, type, field, value => value.NumberValue = 101));

        await SaveItemAsync(test, type, field, value => value.NumberValue = 100);
    }

    [Fact]
    public async Task Ein_leeres_Feld_verstoesst_gegen_keine_Grenze()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(test, f => f.MinValue = 5);

        // „Nicht ausgefüllt“ ist kein falscher Wert — ob es ausgefüllt sein muss, sagt
        // allein der Pflicht-Schalter.
        await SaveItemAsync(test, type, field, _ => { });
    }

    [Fact]
    public async Task Ein_Text_gegen_ein_Muster()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(
            test, f => f.Pattern = "[A-Z]{2}-[0-9]{3}", ContentFieldType.Text);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemAsync(test, type, field, value => value.TextValue = "ab-1"));

        await SaveItemAsync(test, type, field, value => value.TextValue = "AB-123");
    }

    [Fact]
    public async Task Das_Muster_muss_auf_den_ganzen_Wert_passen()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(test, f => f.Pattern = "[0-9]{3}", ContentFieldType.Text);

        // Drei Ziffern irgendwo im Text reichen nicht — sonst hieße „Muster“ nur „enthält“.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemAsync(test, type, field, value => value.TextValue = "abc123def"));
    }

    [Fact]
    public async Task Bei_einer_Stichwortliste_gilt_das_Muster_je_Stichwort()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(
            test,
            f =>
            {
                f.IsTagList = true;
                f.Pattern = "[a-z]+";
            },
            ContentFieldType.Text);

        await SaveItemAsync(test, type, field, value => value.TextValue = "feuer, eis");

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemAsync(test, type, field, value => value.TextValue = "feuer, Eis7"));
    }

    [Fact]
    public async Task Eine_verdrehte_Spanne_wird_schon_am_Feld_abgelehnt()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SeedAsync(test, f =>
            {
                f.MinValue = 10;
                f.MaxValue = 5;
            }));
    }

    [Fact]
    public async Task Ein_kaputtes_Muster_wird_schon_am_Feld_abgelehnt()
    {
        using var test = new TestDatabase();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SeedAsync(test, f => f.Pattern = "[unvollständig", ContentFieldType.Text));
    }

    [Fact]
    public async Task Grenzen_und_Muster_gehen_beim_Typwechsel_verloren()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();
        var (_, field) = await SeedAsync(test, f => f.MinValue = 5);

        // Eine Grenze an einem Textfeld wirkte beim Zurückwechseln unbemerkt weiter —
        // dieselbe Regel wie beim Stichwort-Schalter.
        field.Type = ContentFieldType.Text;
        await types.SaveFieldAsync(field);

        var stored = (await types.GetTypesAsync(test.ProjectId, ModuleKeys.Items))
            .Single()
            .Fields
            .Single();

        Assert.Null(stored.MinValue);
    }

    [Fact]
    public async Task Die_Massenbearbeitung_prueft_dieselben_Grenzen()
    {
        using var test = new TestDatabase();
        var (type, field) = await SeedAsync(test, f => f.MaxValue = 100);

        await SaveItemAsync(test, type, field, value => value.NumberValue = 10);

        await using var db = test.CreateContext();
        var itemId = db.Items.Single().Id;

        // Die Massenbearbeitung darf nicht der Weg sein, die Grenze zu umgehen.
        await Assert.ThrowsAsync<ContentValidationException>(() =>
            test.GetService<BulkEditService>().SetFieldValueAsync(
                test.ProjectId, ModuleKeys.Items, [itemId], field.Id,
                new FieldValue
                {
                    FieldDefinitionId = field.Id,
                    OwnerEntityId = itemId,
                    OwnerModuleKey = ModuleKeys.Items,
                    NumberValue = 500
                }));
    }
}
