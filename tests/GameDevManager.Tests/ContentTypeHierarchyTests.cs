using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Unterarten: „Waffe“ mit den Unterarten Nahkampf/Fernkampf. Eine Unterart erbt die Felder
/// ihrer Eltern-Art — das ist der Grund, warum die Hierarchie an den Arten hängt.
/// </summary>
public class ContentTypeHierarchyTests
{
    /// <summary>Legt „Waffe“ mit dem Feld „Schaden“ an und gibt die Art zurück.</summary>
    private static async Task<ContentType> SeedParentAsync(TestDatabase test, string fieldName = "Schaden")
    {
        var types = test.GetService<ContentTypeService>();

        var parent = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Waffe"
        };

        await types.SaveTypeAsync(parent);
        await types.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = parent.Id,
            ModuleKey = ModuleKeys.Items,
            Name = fieldName,
            Type = ContentFieldType.Integer
        });

        return parent;
    }

    [Fact]
    public async Task Unterart_erbt_die_Felder_der_Eltern_Art()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedParentAsync(test);

        var child = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = parent.Id,
            Name = "Nahkampf"
        };

        await types.SaveTypeAsync(child);
        await types.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = child.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Reichweite",
            Type = ContentFieldType.Decimal
        });

        var loaded = await types.GetTypesAsync(test.ProjectId, ModuleKeys.Items);

        // Die Hierarchie bestimmt die Reihenfolge: Eltern-Art zuerst, Unterart direkt darunter.
        Assert.Equal(["Waffe", "Nahkampf"], loaded.Select(type => type.Name));

        var loadedChild = loaded.Single(type => type.Name == "Nahkampf");
        Assert.Equal("Schaden", Assert.Single(loadedChild.InheritedFields).Name);
        Assert.Equal("Reichweite", Assert.Single(loadedChild.Fields).Name);

        // Die Eltern-Art erbt nichts.
        Assert.Empty(loaded.Single(type => type.Name == "Waffe").InheritedFields);
    }

    [Fact]
    public async Task Geerbte_Felder_gelten_an_der_Entitaet()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedParentAsync(test);
        var child = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = parent.Id,
            Name = "Nahkampf"
        };
        await types.SaveTypeAsync(child);

        await using (var db = test.CreateContext())
        {
            db.Items.Add(new Item
            {
                GameProjectId = test.ProjectId,
                ContentTypeId = child.Id,
                Name = "Schwert"
            });
            await db.SaveChangesAsync();
        }

        var items = test.GetService<ItemService>();
        var itemId = (await items.GetItemsAsync(test.ProjectId)).Single().Id;
        var context = await items.LoadForEditAsync(test.ProjectId, itemId);

        Assert.NotNull(context);

        // Die Maske des Items zeigt das geerbte Feld der Eltern-Art.
        Assert.Contains(context.ApplicableFields, field => field.Name == "Schaden");
    }

    [Fact]
    public async Task Ring_und_fremdes_Modul_werden_abgelehnt()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedParentAsync(test);

        var child = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = parent.Id,
            Name = "Nahkampf"
        };
        await types.SaveTypeAsync(child);

        // Die Eltern-Art zur Unterart ihrer eigenen Unterart machen: das schlösse einen Ring.
        parent.ParentId = child.Id;
        await Assert.ThrowsAsync<ContentValidationException>(() => types.SaveTypeAsync(parent));

        // Eine Art kann auch nicht ihre eigene Unterart sein.
        parent.ParentId = parent.Id;
        await Assert.ThrowsAsync<ContentValidationException>(() => types.SaveTypeAsync(parent));

        // Und die Eltern-Art muss aus demselben Modul stammen.
        var npcType = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Npcs,
            ParentId = parent.Id,
            Name = "Händler"
        };
        await Assert.ThrowsAsync<ContentValidationException>(() => types.SaveTypeAsync(npcType));
    }

    [Fact]
    public async Task Art_mit_Unterarten_laesst_sich_nicht_loeschen()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedParentAsync(test);
        await types.SaveTypeAsync(new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = parent.Id,
            Name = "Nahkampf"
        });

        await Assert.ThrowsAsync<ContentValidationException>(() => types.DeleteTypeAsync(parent.Id));

        await using var db = test.CreateContext();
        Assert.Equal(2, await db.ContentTypes.CountAsync());
    }

    [Fact]
    public async Task Gleicher_Feldname_in_derselben_Linie_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var parent = await SeedParentAsync(test);
        var child = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            ParentId = parent.Id,
            Name = "Nahkampf"
        };
        await types.SaveTypeAsync(child);

        // „Schaden“ steht schon an der Eltern-Art — zwei gleichnamige Felder in einer Maske
        // wären nicht auseinanderzuhalten.
        await Assert.ThrowsAsync<ContentValidationException>(() => types.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = child.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "schaden",
            Type = ContentFieldType.Integer
        }));

        // Eine Art außerhalb der Linie darf denselben Namen tragen.
        var other = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Rüstung"
        };
        await types.SaveTypeAsync(other);
        await types.SaveFieldAsync(new FieldDefinition
        {
            ContentTypeId = other.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Schaden",
            Type = ContentFieldType.Integer
        });
    }
}
