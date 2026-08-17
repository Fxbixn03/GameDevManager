using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Gespeicherte Suchen und Listenansichten (F27). Eine Seite für alle Module statt einer
/// Filterleiste in jeder der gut zwanzig Modul-Listen — dieselbe Überlegung wie bei der
/// Massenbearbeitung; gefiltert wird über die <c>IModuleEntitySource</c>.
/// </summary>
public class SavedViewTests
{
    private sealed record Fixture(ContentType Weapon, ContentType Melee, FieldDefinition Damage);

    /// <summary>
    /// Ein Konto anlegen — eine gespeicherte Ansicht gehört einem Benutzer, und ihr
    /// Fremdschlüssel braucht eine echte Zeile.
    /// </summary>
    private static async Task SeedUserAsync(TestDatabase test, string name = "alrik")
    {
        await using var db = test.CreateContext();

        var user = new AppUser { UserName = name, DisplayName = name, PasswordHash = string.Empty };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        test.Author.Current = new ChangeAuthor(user.Id, name);
    }

    private static async Task<Fixture> SeedAsync(TestDatabase test)
    {
        var types = test.GetService<ContentTypeService>();

        var weapon = new ContentType
        {
            GameProjectId = test.ProjectId, ModuleKey = ModuleKeys.Items, Name = "Waffe"
        };
        await types.SaveTypeAsync(weapon);

        var melee = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Nahkampf",
            ParentId = weapon.Id
        };
        await types.SaveTypeAsync(melee);

        var damage = new FieldDefinition
        {
            ModuleKey = ModuleKeys.Items,
            ContentTypeId = weapon.Id,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        };
        await types.SaveFieldAsync(damage);

        return new Fixture(weapon, melee, damage);
    }

    private static async Task<Item> SaveItemAsync(
        TestDatabase test, string name, Guid? typeId = null,
        ContentStatus status = ContentStatus.Draft,
        (FieldDefinition Field, double Value)? value = null)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        context.Entity.ContentTypeId = typeId;
        context.Entity.Status = status;

        if (value is { } pair)
        {
            context.ValueFor(pair.Field).NumberValue = pair.Value;
        }

        await items.SaveItemAsync(context);
        return context.Entity;
    }

    [Fact]
    public async Task Der_Textfilter_sucht_in_Name_und_Beschreibung()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Eisenschwert");
        await SaveItemAsync(test, "Holzschild");

        var rows = await test.GetService<SavedViewService>().QueryAsync(
            test.ProjectId, ModuleKeys.Items, new ContentFilter { Text = "schwert" }, []);

        Assert.Equal("Eisenschwert", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Der_Artfilter_nimmt_Unterarten_mit()
    {
        using var test = new TestDatabase();
        var fixture = await SeedAsync(test);

        await SaveItemAsync(test, "Schwert", fixture.Melee.Id);
        await SaveItemAsync(test, "Trank");

        var views = test.GetService<SavedViewService>();

        // Wer „Waffe“ filtert, meint fast immer auch „Nahkampf“ — sonst zeigte der Filter
        // ausgerechnet den Bestand nicht, den die Feldvererbung zusammenhält.
        var withSubtypes = await views.QueryAsync(
            test.ProjectId, ModuleKeys.Items,
            new ContentFilter { ContentTypeId = fixture.Weapon.Id, IncludeSubtypes = true }, []);

        Assert.Equal("Schwert", Assert.Single(withSubtypes).Name);

        // Und wer es ausdrücklich nicht will, bekommt nur die Art selbst.
        var withoutSubtypes = await views.QueryAsync(
            test.ProjectId, ModuleKeys.Items,
            new ContentFilter { ContentTypeId = fixture.Weapon.Id, IncludeSubtypes = false }, []);

        Assert.Empty(withoutSubtypes);
    }

    [Fact]
    public async Task Der_Standfilter_nimmt_mehrere_Staende()
    {
        using var test = new TestDatabase();

        await SaveItemAsync(test, "Entwurf", status: ContentStatus.Draft);
        await SaveItemAsync(test, "Review", status: ContentStatus.InReview);
        await SaveItemAsync(test, "Fertig", status: ContentStatus.Done);

        var rows = await test.GetService<SavedViewService>().QueryAsync(
            test.ProjectId, ModuleKeys.Items,
            new ContentFilter { Statuses = [ContentStatus.InReview, ContentStatus.Done] }, []);

        Assert.Equal(["Fertig", "Review"], rows.Select(row => row.Name).Order().ToArray());
    }

    [Fact]
    public async Task Eine_Feldbedingung_vergleicht_Zahlen()
    {
        using var test = new TestDatabase();
        var fixture = await SeedAsync(test);

        await SaveItemAsync(test, "Dolch", fixture.Weapon.Id, value: (fixture.Damage, 5));
        await SaveItemAsync(test, "Zweihänder", fixture.Weapon.Id, value: (fixture.Damage, 80));

        var filter = new ContentFilter
        {
            Fields =
            [
                new FieldCriterion
                {
                    FieldDefinitionId = fixture.Damage.Id,
                    Comparison = FieldComparison.GreaterThan,
                    Value = "50"
                }
            ]
        };

        var rows = await test.GetService<SavedViewService>().QueryAsync(
            test.ProjectId, ModuleKeys.Items, filter, [fixture.Damage.Id]);

        var row = Assert.Single(rows);
        Assert.Equal("Zweihänder", row.Name);

        // Die gewählte Spalte kommt mit — das ist die zweite Hälfte der Userstory.
        Assert.Equal(80, row.Values[fixture.Damage.Id].NumberValue);
    }

    [Fact]
    public async Task Leer_und_gefuellt_sind_eigene_Vergleiche()
    {
        using var test = new TestDatabase();
        var fixture = await SeedAsync(test);

        await SaveItemAsync(test, "Ohne Wert", fixture.Weapon.Id);
        await SaveItemAsync(test, "Mit Wert", fixture.Weapon.Id, value: (fixture.Damage, 7));

        var views = test.GetService<SavedViewService>();

        ContentFilter FilterFor(FieldComparison comparison) => new()
        {
            Fields = [new FieldCriterion { FieldDefinitionId = fixture.Damage.Id, Comparison = comparison }]
        };

        var empty = await views.QueryAsync(test.ProjectId, ModuleKeys.Items, FilterFor(FieldComparison.IsEmpty), []);
        Assert.Equal("Ohne Wert", Assert.Single(empty).Name);

        var filled = await views.QueryAsync(test.ProjectId, ModuleKeys.Items, FilterFor(FieldComparison.IsNotEmpty), []);
        Assert.Equal("Mit Wert", Assert.Single(filled).Name);
    }

    [Fact]
    public async Task Ohne_Sprite_findet_was_kein_Icon_hat()
    {
        using var test = new TestDatabase();

        var withIcon = await SaveItemAsync(test, "Mit Icon");
        await SaveItemAsync(test, "Ohne Icon");

        await test.GetService<AssetService>().UploadAsync(
            test.ProjectId, "icon.png", "image/png", new MemoryStream([1, 2, 3]),
            ModuleKeys.Items, withIcon.Id);

        var rows = await test.GetService<SavedViewService>().QueryAsync(
            test.ProjectId, ModuleKeys.Items, new ContentFilter { WithoutSprite = true }, []);

        Assert.Equal("Ohne Icon", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Nur_Varianten_zeigt_was_ein_Vorbild_hat()
    {
        using var test = new TestDatabase();

        var basis = await SaveItemAsync(test, "Eisenschwert");

        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert +1";
        context.Entity.BasedOnId = basis.Id;
        await items.SaveItemAsync(context);

        var rows = await test.GetService<SavedViewService>().QueryAsync(
            test.ProjectId, ModuleKeys.Items, new ContentFilter { OnlyVariants = true }, []);

        Assert.Equal("Eisenschwert +1", Assert.Single(rows).Name);
    }

    [Fact]
    public async Task Eine_Ansicht_laesst_sich_speichern_und_wiederfinden()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var fixture = await SeedAsync(test);
        var views = test.GetService<SavedViewService>();

        var filter = new ContentFilter
        {
            ContentTypeId = fixture.Weapon.Id,
            Statuses = [ContentStatus.Draft],
            WithoutSprite = true
        };

        await views.SaveViewAsync(
            test.ProjectId, null, ModuleKeys.Items, "Waffen ohne Bild", filter, [fixture.Damage.Id]);

        var stored = Assert.Single(await views.GetViewsAsync(test.ProjectId));

        Assert.Equal("Waffen ohne Bild", stored.Name);
        Assert.Equal(fixture.Weapon.Id, stored.Filter.ContentTypeId);
        Assert.True(stored.Filter.WithoutSprite);
        Assert.Equal(ContentStatus.Draft, Assert.Single(stored.Filter.Statuses));
        Assert.Equal(fixture.Damage.Id, Assert.Single(stored.ColumnFieldIds));
    }

    [Fact]
    public async Task Der_Name_ist_je_Modul_eindeutig()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var views = test.GetService<SavedViewService>();

        await views.SaveViewAsync(test.ProjectId, null, ModuleKeys.Items, "Offen", new ContentFilter(), []);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => views.SaveViewAsync(test.ProjectId, null, ModuleKeys.Items, "Offen", new ContentFilter(), []));

        // In einem anderen Modul ist derselbe Name frei — es ist eine andere Liste.
        await views.SaveViewAsync(test.ProjectId, null, ModuleKeys.Npcs, "Offen", new ContentFilter(), []);
        Assert.Equal(2, (await views.GetViewsAsync(test.ProjectId)).Count);
    }

    [Fact]
    public async Task Die_aufgeloesten_Unterarten_stehen_nicht_im_gespeicherten_Filter()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var fixture = await SeedAsync(test);
        var views = test.GetService<SavedViewService>();

        var filter = new ContentFilter { ContentTypeId = fixture.Weapon.Id };

        // Einmal auswerten — dabei füllt der Dienst die aufgelösten Arten.
        await views.QueryAsync(test.ProjectId, ModuleKeys.Items, filter, []);
        Assert.Equal(2, filter.ExpandedTypeIds.Count);

        await views.SaveViewAsync(test.ProjectId, null, ModuleKeys.Items, "Waffen", filter, []);

        // Gespeichert wird nur die gewählte Art: Wer später eine Unterart anlegt, soll sie in
        // seiner Ansicht wiederfinden, ohne sie neu zu wählen.
        var stored = Assert.Single(await views.GetViewsAsync(test.ProjectId));
        Assert.Empty(stored.Filter.ExpandedTypeIds);
        Assert.Equal(fixture.Weapon.Id, stored.Filter.ContentTypeId);
    }

    [Fact]
    public async Task Ansichten_lassen_sich_loeschen()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test);
        var views = test.GetService<SavedViewService>();

        await views.SaveViewAsync(test.ProjectId, null, ModuleKeys.Items, "Offen", new ContentFilter(), []);
        var stored = Assert.Single(await views.GetViewsAsync(test.ProjectId));

        await views.DeleteViewAsync(stored.Id);
        Assert.Empty(await views.GetViewsAsync(test.ProjectId));
    }
}
