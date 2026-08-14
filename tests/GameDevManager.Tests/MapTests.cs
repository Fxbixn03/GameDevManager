using System.Globalization;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Polygon-Gebiete der Karten — „Gebiete der Fraktionen einzeichnen“ aus dem Konzept.
/// Geprüft werden die kanonische Punktliste, die Validierung (mindestens drei lesbare Punkte
/// im Bild) und dass ein Gebiet das Duplizieren eines Projekts übersteht.
/// </summary>
public class MapTests
{
    [Fact]
    public async Task Ein_Polygon_Gebiet_wird_kanonisch_gespeichert_und_verdraengt_den_Radius()
    {
        using var test = new TestDatabase();
        var maps = test.GetService<MapService>();

        var context = await maps.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Weltkarte";
        context.Entity.Markers.Add(new MapMarker
        {
            MapId = context.Entity.Id,
            X = 0.3,
            Y = 0.4,
            // Radius und Polygon zusammen ergäben zwei Aussagen — das Polygon gewinnt.
            Radius = 0.2,
            Points = " 0.1,0.1; 0.50,0.1 ;0.30000,0.6 ",
            Label = "Fraktionsgebiet"
        });

        await maps.SaveMapAsync(context);

        var reloaded = await maps.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        var marker = Assert.Single(reloaded!.Entity.Markers);

        Assert.True(marker.IsPolygon);
        Assert.False(marker.IsArea);
        Assert.Null(marker.Radius);

        // Kanonische Schreibweise: feste Kultur, feste Rundung, keine Leerzeichen — derselbe
        // Stand ergibt denselben Export.
        Assert.Equal("0.1,0.1;0.5,0.1;0.3,0.6", marker.Points);
    }

    [Fact]
    public async Task Ein_Gebiet_braucht_mindestens_drei_lesbare_Punkte()
    {
        using var test = new TestDatabase();
        var maps = test.GetService<MapService>();

        // Zwei Punkte sind eine Linie, kein Gebiet.
        await Assert.ThrowsAsync<ContentValidationException>(() =>
            SaveWithPointsAsync(maps, test.ProjectId, "0.1,0.1;0.2,0.2"));

        // Eine unlesbare Liste läuft in dieselbe Meldung, statt halb gespeichert zu werden.
        await Assert.ThrowsAsync<ContentValidationException>(() =>
            SaveWithPointsAsync(maps, test.ProjectId, "kein;polygon"));
    }

    [Fact]
    public async Task Eckpunkte_ausserhalb_des_Bildes_werden_abgelehnt()
    {
        using var test = new TestDatabase();
        var maps = test.GetService<MapService>();

        await Assert.ThrowsAsync<ContentValidationException>(() =>
            SaveWithPointsAsync(maps, test.ProjectId, "0,0;2,0;1,1"));
    }

    [Fact]
    public void Punktlisten_lesen_und_schreiben_in_fester_Kultur()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            // Unter deutscher Kultur wäre das Komma sonst Dezimal- und Listentrenner zugleich.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var text = MapMarker.FormatPoints([new(0.125, 0.5), new(0.25, 0.75), new(1, 0)]);

            Assert.Equal("0.125,0.5;0.25,0.75;1,0", text);
            Assert.Equal(3, MapMarker.ParsePoints(text).Count);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Ein_Gebiet_uebersteht_das_Duplizieren_eines_Projekts()
    {
        using var test = new TestDatabase();
        var maps = test.GetService<MapService>();

        await SaveWithPointsAsync(maps, test.ProjectId, "0.1,0.1;0.9,0.1;0.5,0.8");

        var copy = await test.GetService<ProjectService>()
            .DuplicateProjectAsync(test.ProjectId, "Kopie", null);

        var row = Assert.Single(await maps.GetMapsAsync(copy.Id));
        var copied = await maps.LoadForEditAsync(copy.Id, row.Id);
        var marker = Assert.Single(copied!.Entity.Markers);

        Assert.Equal("0.1,0.1;0.9,0.1;0.5,0.8", marker.Points);
    }

    private static async Task SaveWithPointsAsync(MapService maps, Guid projectId, string points)
    {
        var context = await maps.LoadForEditAsync(projectId, null);
        context!.Entity.Name = "Karte";
        context.Entity.Markers.Add(new MapMarker
        {
            MapId = context.Entity.Id,
            X = 0.5,
            Y = 0.5,
            Points = points
        });

        await maps.SaveMapAsync(context);
    }
}
