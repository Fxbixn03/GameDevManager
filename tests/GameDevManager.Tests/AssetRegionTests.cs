using System.Buffers.Binary;
using System.Text.Json;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Ausschnitte an einem Asset: die Zellen eines Sprite-Sheets. Das Tool schneidet nicht, es
/// verwaltet nur die Rechtecke — gemessen in Pixeln, weil die Engine sie so erwartet.
/// </summary>
public class AssetRegionTests
{
    /// <summary>
    /// Ein PNG-Kopf mit den gewünschten Maßen. Der <c>ImageDimensionReader</c> liest nur
    /// Signatur und IHDR-Block, also reicht genau der — ein echtes Bild bräuchte eine
    /// Bildbibliothek, die das Projekt bewusst nicht hat.
    /// </summary>
    private static MemoryStream PngHeader(int width, int height)
    {
        var bytes = new byte[24];

        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);

        return new MemoryStream(bytes);
    }

    private static Task<Asset> UploadSheetAsync(
        TestDatabase test, int width = 128, int height = 64, string fileName = "held.png") =>
        test.GetService<AssetService>().UploadAsync(
            test.ProjectId, fileName, "image/png", PngHeader(width, height));

    [Fact]
    public async Task Das_Raster_zaehlt_zeilenweise_von_links_oben()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test, width: 128, height: 64);

        var regions = await assets.BuildGridAsync(sheet.Id, cellWidth: 32, cellHeight: 32);

        // 4 Spalten × 2 Zeilen.
        Assert.Equal(8, regions.Count);
        Assert.Equal((0, 0), (regions[0].X, regions[0].Y));
        Assert.Equal((32, 0), (regions[1].X, regions[1].Y));
        Assert.Equal((0, 32), (regions[4].X, regions[4].Y));

        // Der Name kommt aus dem Dateinamen, damit er in der Engine wiederzuerkennen ist.
        Assert.Equal("held_0", regions[0].Name);
    }

    [Fact]
    public async Task Eine_Zelle_ueber_dem_Bildrand_entsteht_nicht()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test, width: 100, height: 32);

        var regions = await assets.BuildGridAsync(sheet.Id, cellWidth: 32, cellHeight: 32);

        // 100 / 32 = 3 volle Zellen; der Reststreifen von 4 Pixeln ist Rest, nicht Inhalt.
        Assert.Equal(3, regions.Count);
    }

    [Fact]
    public async Task Rand_und_Abstand_wirken_auf_das_Raster()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test, width: 70, height: 32);

        var regions = await assets.BuildGridAsync(
            sheet.Id, cellWidth: 32, cellHeight: 32, offsetX: 2, offsetY: 0, spacingX: 4);

        Assert.Equal(2, regions.Count);
        Assert.Equal(2, regions[0].X);
        Assert.Equal(38, regions[1].X);
    }

    [Fact]
    public async Task Ohne_bekannte_Masse_gibt_es_kein_Raster()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();

        // Ein SVG trägt seine Maße nicht im Dateikopf — der Leser liefert nichts.
        var sheet = await assets.UploadAsync(
            test.ProjectId, "karte.svg", "image/svg+xml", new MemoryStream([1, 2, 3]));

        await Assert.ThrowsAsync<ContentValidationException>(
            () => assets.BuildGridAsync(sheet.Id, 32, 32));
    }

    [Fact]
    public async Task Passt_keine_Zelle_ins_Bild_meldet_sich_das_Raster()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test, width: 16, height: 16);

        await Assert.ThrowsAsync<ContentValidationException>(
            () => assets.BuildGridAsync(sheet.Id, 32, 32));
    }

    [Fact]
    public async Task Speichern_legt_an_aendert_und_entfernt()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test);

        var built = await assets.BuildGridAsync(sheet.Id, 32, 32);
        await assets.SaveRegionsAsync(sheet.Id, built);

        var stored = await assets.GetRegionsAsync(sheet.Id);
        Assert.Equal(8, stored.Count);

        // Der zweite bleibt, umbenannt; alle übrigen fallen weg.
        stored[1].Name = "lauf_rechts";
        await assets.SaveRegionsAsync(sheet.Id, [stored[1]]);

        var after = await assets.GetRegionsAsync(sheet.Id);
        var single = Assert.Single(after);
        Assert.Equal("lauf_rechts", single.Name);
        Assert.Equal(stored[1].Id, single.Id);
    }

    [Fact]
    public async Task Ein_Ausschnitt_ausserhalb_des_Bildes_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test, width: 64, height: 64);

        var region = new AssetRegion { AssetId = sheet.Id, Name = "zuweit", X = 40, Y = 0, Width = 32, Height = 32 };

        await Assert.ThrowsAsync<ContentValidationException>(
            () => assets.SaveRegionsAsync(sheet.Id, [region]));
    }

    [Fact]
    public async Task Zwei_gleiche_Namen_werden_abgelehnt()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test);

        AssetRegion Make(string name, int x) =>
            new() { AssetId = sheet.Id, Name = name, X = x, Y = 0, Width = 32, Height = 32 };

        // Der Name wird in der Engine zum Bezeichner — auch mit anderer Schreibweise ist er derselbe.
        await Assert.ThrowsAsync<ContentValidationException>(
            () => assets.SaveRegionsAsync(sheet.Id, [Make("Lauf", 0), Make("lauf", 32)]));
    }

    [Fact]
    public async Task Ein_Ausschnitt_ohne_Flaeche_wird_abgelehnt()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test);

        var region = new AssetRegion { AssetId = sheet.Id, Name = "leer", X = 0, Y = 0, Width = 0, Height = 32 };

        await Assert.ThrowsAsync<ContentValidationException>(
            () => assets.SaveRegionsAsync(sheet.Id, [region]));
    }

    [Fact]
    public void Ausschnitte_stehen_im_Export_die_frueheren_Fassungen_nicht()
    {
        var asset = new Asset
        {
            GameProjectId = Guid.NewGuid(),
            FileName = "held.png",
            MimeType = "image/png",
            StorageKey = "p/a.png"
        };

        asset.Regions.Add(new AssetRegion
        {
            AssetId = asset.Id, Name = "held_0", X = 0, Y = 0, Width = 32, Height = 32
        });

        var json = JsonSerializer.Serialize(asset, ExportFormat.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Die Engine braucht die Rechtecke — sie sind der ganze Zweck der Ausschnitte.
        var region = root.GetProperty("regions")[0];
        Assert.Equal("held_0", region.GetProperty("name").GetString());
        Assert.Equal(32, region.GetProperty("width").GetInt32());

        // Die Fassungen sind Werkzeug-Daten wie das Änderungsprotokoll; ungenannt stünden sie
        // als immer leere Liste im Archiv und sähen nach „keine vorhanden“ aus.
        Assert.False(root.TryGetProperty("versions", out _));
    }

    [Fact]
    public async Task Beim_Loeschen_des_Assets_gehen_die_Ausschnitte_mit()
    {
        using var test = new TestDatabase();
        var assets = test.GetService<AssetService>();
        var sheet = await UploadSheetAsync(test);

        await assets.SaveRegionsAsync(sheet.Id, await assets.BuildGridAsync(sheet.Id, 32, 32));
        await assets.DeleteAsync(sheet.Id);

        await using var db = test.CreateContext();
        Assert.Empty(await db.AssetRegions.ToListAsync());
    }
}
