using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Curves;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Kurven-Sammlung hinter dem Vergleich zweier Levelkurven: Sie muss modulübergreifend
/// finden, was im Projekt an Kurven gefüllt ist — und nichts aus einem fremden Projekt,
/// denn Feldwerte tragen keine Projekt-Spalte.
/// </summary>
public class CurveLibraryTests
{
    /// <summary>Legt ein Item mit einem Kurvenfeld an und schreibt die Kurve hinein.</summary>
    private static async Task<(Guid ItemId, Guid FieldId)> SeedCurveAsync(
        TestDatabase database, Guid projectId, string itemName, string fieldName, string expression)
    {
        await using var db = database.CreateContext();

        var type = new ContentType { GameProjectId = projectId, ModuleKey = ModuleKeys.Items, Name = $"Art {itemName}" };
        var field = new FieldDefinition
        {
            ContentTypeId = type.Id,
            ModuleKey = ModuleKeys.Items,
            Name = fieldName,
            Type = ContentFieldType.Curve
        };

        var item = new Item { GameProjectId = projectId, ContentTypeId = type.Id, Name = itemName };

        var curve = new CurveDefinition { Expression = expression, From = 1, To = 5, Step = 1 };
        var value = new FieldValue
        {
            OwnerEntityId = item.Id,
            OwnerModuleKey = ModuleKeys.Items,
            FieldDefinitionId = field.Id,
            TextValue = curve.Serialize()
        };

        db.ContentTypes.Add(type);
        db.FieldDefinitions.Add(field);
        db.Items.Add(item);
        db.FieldValues.Add(value);
        await db.SaveChangesAsync();

        return (item.Id, field.Id);
    }

    [Fact]
    public async Task Gefundene_Kurven_tragen_Besitzer_Feld_und_Wert()
    {
        using var database = new TestDatabase();
        var (itemId, fieldId) = await SeedCurveAsync(
            database, database.ProjectId, "Schwert", "Schaden", "10 * x");

        var found = Assert.Single(await database.GetService<CurveService>().GetCurvesAsync(database.ProjectId));

        Assert.Equal(itemId, found.OwnerEntityId);
        Assert.Equal(ModuleKeys.Items, found.OwnerModuleKey);
        Assert.Equal("Schwert", found.OwnerName);
        Assert.Equal(fieldId, found.FieldDefinitionId);
        Assert.Equal("Schaden", found.FieldName);

        // Der gespeicherte Text muss sich wieder zur Kurve lesen lassen — die Oberfläche
        // zeichnet daraus die zweite Linie.
        var curve = CurveDefinition.Parse(found.Stored);
        Assert.NotNull(curve);
        Assert.Equal(50, curve!.Sample()[^1].Y);
    }

    [Fact]
    public async Task Kurven_eines_fremden_Projekts_werden_nicht_angeboten()
    {
        using var database = new TestDatabase();

        var other = new GameProject { Name = "Zweitprojekt" };
        await database.GetService<ProjectService>().SaveProjectAsync(other);

        await SeedCurveAsync(database, database.ProjectId, "Schwert", "Schaden", "10 * x");
        await SeedCurveAsync(database, other.Id, "Fremdes Schwert", "Schaden", "20 * x");

        var found = Assert.Single(await database.GetService<CurveService>().GetCurvesAsync(database.ProjectId));

        Assert.Equal("Schwert", found.OwnerName);
    }

    [Fact]
    public async Task Mehrere_Kurven_kommen_nach_Besitzer_und_Feld_sortiert()
    {
        using var database = new TestDatabase();

        await SeedCurveAsync(database, database.ProjectId, "Zauberstab", "Schaden", "5 * x");
        await SeedCurveAsync(database, database.ProjectId, "Axt", "Wucht", "8 * x");
        await SeedCurveAsync(database, database.ProjectId, "Axt", "Abnutzung", "x");

        var found = await database.GetService<CurveService>().GetCurvesAsync(database.ProjectId);

        Assert.Equal(
            new[] { ("Axt", "Abnutzung"), ("Axt", "Wucht"), ("Zauberstab", "Schaden") },
            found.Select(curve => (curve.OwnerName, curve.FieldName)));
    }

    [Fact]
    public async Task Felder_ohne_Kurve_bleiben_draussen()
    {
        using var database = new TestDatabase();
        await SeedCurveAsync(database, database.ProjectId, "Schwert", "Schaden", "10 * x");

        await using (var db = database.CreateContext())
        {
            var type = new ContentType
            {
                GameProjectId = database.ProjectId,
                ModuleKey = ModuleKeys.Items,
                Name = "Trank"
            };
            // Ein Textfeld mit Inhalt und ein leeres Kurvenfeld: Beides ist keine Kurve, die
            // sich zeichnen ließe.
            var text = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Notiz",
                Type = ContentFieldType.Text
            };
            var emptyCurve = new FieldDefinition
            {
                ContentTypeId = type.Id,
                ModuleKey = ModuleKeys.Items,
                Name = "Wirkung",
                Type = ContentFieldType.Curve
            };
            var item = new Item { GameProjectId = database.ProjectId, ContentTypeId = type.Id, Name = "Heiltrank" };

            db.ContentTypes.Add(type);
            db.FieldDefinitions.AddRange(text, emptyCurve);
            db.Items.Add(item);
            db.FieldValues.AddRange(
                new FieldValue
                {
                    OwnerEntityId = item.Id,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = text.Id,
                    TextValue = "kein Kurven-JSON"
                },
                new FieldValue
                {
                    OwnerEntityId = item.Id,
                    OwnerModuleKey = ModuleKeys.Items,
                    FieldDefinitionId = emptyCurve.Id,
                    TextValue = null
                });
            await db.SaveChangesAsync();
        }

        var found = Assert.Single(await database.GetService<CurveService>().GetCurvesAsync(database.ProjectId));

        Assert.Equal("Schaden", found.FieldName);
    }

    [Fact]
    public async Task Ein_Projekt_ohne_Kurven_liefert_eine_leere_Liste()
    {
        using var database = new TestDatabase();

        Assert.Empty(await database.GetService<CurveService>().GetCurvesAsync(database.ProjectId));
    }
}
