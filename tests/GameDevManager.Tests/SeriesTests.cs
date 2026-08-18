using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Serien-Anlage: viele Entwürfe auf einmal, ein Sammeleintrag im Änderungsprotokoll —
/// und die Leitplanken, die einen vertippten Lauf abfangen.
/// </summary>
public class SeriesTests
{
    [Fact]
    public async Task Eine_Serie_legt_Entwuerfe_mit_Art_und_einem_Sammeleintrag_an()
    {
        using var test = new TestDatabase();

        Guid typeId;
        await using (var db = test.CreateContext())
        {
            var type = new ContentType
            {
                GameProjectId = test.ProjectId,
                ModuleKey = ModuleKeys.Items,
                Name = "Waffe"
            };
            typeId = type.Id;
            db.ContentTypes.Add(type);
            await db.SaveChangesAsync();
        }

        var created = await test.GetService<SeriesService>()
            .CreateAsync(test.ProjectId, ModuleKeys.Items, typeId, "{liste:Eisen|Stahl}schwert {n:01}", 4);

        Assert.Equal(4, created.Count);

        await using (var db = test.CreateContext())
        {
            var items = await db.Items
                .Where(item => item.GameProjectId == test.ProjectId)
                .OrderBy(item => item.Name)
                .ToListAsync();

            Assert.Equal(
                ["Eisenschwert 01", "Eisenschwert 03", "Stahlschwert 02", "Stahlschwert 04"],
                items.Select(item => item.Name).ToList());
            Assert.All(items, item => Assert.Equal(ContentStatus.Draft, item.Status));
            Assert.All(items, item => Assert.Equal(typeId, item.ContentTypeId));

            // Ein Lauf ist ein Eintrag, keine vier — wie beim Import. (Die eine Zeile der
            // Art-Anlage oben gehört nicht zur Serie.)
            var entry = Assert.Single(await db.ChangeLogEntries
                .Where(e => e.ModuleKey == ModuleKeys.Changelog)
                .ToListAsync());
            Assert.Contains("Eisenschwert 01", entry.Details);
        }
    }

    [Fact]
    public async Task Ohne_Platzhalter_wird_die_Nummer_angehaengt()
    {
        using var test = new TestDatabase();

        await test.GetService<SeriesService>()
            .CreateAsync(test.ProjectId, ModuleKeys.Items, null, "Fackel", 3);

        await using var db = test.CreateContext();

        Assert.Equal(
            ["Fackel 1", "Fackel 2", "Fackel 3"],
            await db.Items.OrderBy(item => item.Name).Select(item => item.Name).ToListAsync());
    }

    [Fact]
    public async Task Leitplanken_fangen_Anzahl_Vorlage_und_fremde_Art_ab()
    {
        using var test = new TestDatabase();
        var series = test.GetService<SeriesService>();

        await Assert.ThrowsAsync<ContentValidationException>(
            () => series.CreateAsync(test.ProjectId, ModuleKeys.Items, null, " ", 3));
        await Assert.ThrowsAsync<ContentValidationException>(
            () => series.CreateAsync(test.ProjectId, ModuleKeys.Items, null, "Fackel {n}", 0));
        await Assert.ThrowsAsync<ContentValidationException>(
            () => series.CreateAsync(test.ProjectId, ModuleKeys.Items, null, "Fackel {n}", SeriesService.MaxCount + 1));

        // Eine Art aus einem fremden Modul gehört nicht in die Serie.
        Guid npcTypeId;
        await using (var db = test.CreateContext())
        {
            var npcType = new ContentType
            {
                GameProjectId = test.ProjectId,
                ModuleKey = ModuleKeys.Npcs,
                Name = "Händler"
            };
            npcTypeId = npcType.Id;
            db.ContentTypes.Add(npcType);
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<ContentValidationException>(
            () => series.CreateAsync(test.ProjectId, ModuleKeys.Items, npcTypeId, "Fackel {n}", 3));

        // Diplomatie erlaubt kein Kopieren — und damit auch keine Serie.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => series.CreateAsync(test.ProjectId, ModuleKeys.Diplomacy, null, "Pakt {n}", 3));
    }

    [Fact]
    public async Task Ohne_Schreibrecht_entsteht_keine_Serie()
    {
        using var test = new TestDatabase();
        test.Permissions.Current = UserPermissions.Full with { IsAdministrator = false, CanWrite = false };

        await Assert.ThrowsAsync<ContentValidationException>(() => test.GetService<SeriesService>()
            .CreateAsync(test.ProjectId, ModuleKeys.Items, null, "Fackel {n}", 3));

        await using var db = test.CreateContext();
        Assert.Empty(await db.Items.ToListAsync());
    }
}
