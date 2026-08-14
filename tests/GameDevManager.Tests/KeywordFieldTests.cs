using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Textfelder mit dem Schalter „Als Stichwörter“: mehrere Werte in einem Feld (die Elemente
/// eines Zaubers), erfasst als Chips und gespeichert als kanonische, kommagetrennte Textspalte.
/// <para>
/// Geprüft wird über <see cref="ItemService"/> stellvertretend für alle Modul-Dienste — die
/// Kanonisierung sitzt in <see cref="ContentFields"/>, durch das jeder von ihnen läuft.
/// </para>
/// </summary>
public class KeywordFieldTests
{
    /// <summary>Legt die Art „Zauber“ mit einem Stichwortfeld an und gibt das Feld zurück.</summary>
    private static async Task<FieldDefinition> SeedKeywordFieldAsync(
        TestDatabase test, bool required = false)
    {
        var types = test.GetService<ContentTypeService>();

        var type = new ContentType
        {
            GameProjectId = test.ProjectId,
            ModuleKey = ModuleKeys.Items,
            Name = "Zauber"
        };
        await types.SaveTypeAsync(type);

        var field = new FieldDefinition
        {
            ContentTypeId = type.Id,
            ModuleKey = ModuleKeys.Items,
            Name = "Elemente",
            Type = ContentFieldType.Text,
            IsTagList = true,
            IsRequired = required
        };
        await types.SaveFieldAsync(field);

        return field;
    }

    /// <summary>Legt ein Item der Art an und trägt den Rohtext in das Stichwortfeld ein.</summary>
    private static async Task<Guid> SaveItemWithKeywordsAsync(
        TestDatabase test, FieldDefinition field, string? raw)
    {
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Feuerball";
        context.Entity.ContentTypeId = field.ContentTypeId;
        context.ValueFor(context.ApplicableFields.Single(f => f.Id == field.Id)).TextValue = raw;

        await items.SaveItemAsync(context);
        return context.Entity.Id;
    }

    [Fact]
    public void Stichwoerter_werden_kanonisch_geschrieben()
    {
        // Getrimmt, ohne Leereinträge, ohne Dubletten — derselbe Stand ergibt denselben Export.
        Assert.Equal("Feuer, Eis, Wasser", KeywordList.Normalize("  Feuer ,Eis,, feuer , Wasser "));

        // Nichts Verwertbares ergibt null statt einer leeren Zeichenkette.
        Assert.Null(KeywordList.Normalize(" , ,, "));
        Assert.Null(KeywordList.Normalize(null));

        Assert.Equal(["Feuer", "Eis"], KeywordList.Parse("Feuer, Eis, feuer"));
    }

    [Fact]
    public async Task Ein_Stichwortfeld_speichert_seine_Werte_aufgeraeumt()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var field = await SeedKeywordFieldAsync(test);
        var itemId = await SaveItemWithKeywordsAsync(test, field, "  Feuer ,Eis,, feuer , Wasser ");

        var reloaded = await items.LoadForEditAsync(test.ProjectId, itemId);
        var stored = reloaded!.ValueFor(reloaded.ApplicableFields.Single(f => f.Id == field.Id));

        Assert.Equal("Feuer, Eis, Wasser", stored.TextValue);
    }

    [Fact]
    public async Task Eine_Eingabe_ohne_Stichwort_gilt_als_leer()
    {
        using var test = new TestDatabase();

        var field = await SeedKeywordFieldAsync(test);
        await SaveItemWithKeywordsAsync(test, field, " , ,, ");

        // Aus lauter Kommas wird kein Wert: In der Datenbank bleibt keine Zeile ohne Inhalt
        // stehen, die in Export und Referenzansicht wieder auftauchte.
        await using var db = test.CreateContext();
        Assert.Empty(await db.FieldValues.ToListAsync());
    }

    [Fact]
    public async Task Ein_Pflicht_Stichwortfeld_verlangt_ein_echtes_Stichwort()
    {
        using var test = new TestDatabase();

        var field = await SeedKeywordFieldAsync(test, required: true);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => SaveItemWithKeywordsAsync(test, field, " , ,, "));

        // Mit Inhalt geht dasselbe Feld durch.
        await SaveItemWithKeywordsAsync(test, field, "Feuer");
    }

    [Fact]
    public async Task Der_Schalter_gilt_nur_am_Textfeld()
    {
        using var test = new TestDatabase();
        var types = test.GetService<ContentTypeService>();

        var field = await SeedKeywordFieldAsync(test);

        // Umgestellt auf eine Zahl: Der Schalter darf nicht stehen bleiben, sonst wirkte er
        // beim Zurückwechseln unbemerkt weiter.
        field.Type = ContentFieldType.Integer;
        await types.SaveFieldAsync(field);

        await using var db = test.CreateContext();
        var stored = await db.FieldDefinitions.SingleAsync(f => f.Id == field.Id);

        Assert.False(stored.IsTagList);
        Assert.False(stored.IsKeywordField);
    }
}
