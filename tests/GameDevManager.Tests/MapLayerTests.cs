using Xunit;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>Ebenen der Karten: Zuordnung der Markierungen, Sichtbarkeit, Aufräumen.</summary>
public sealed class MapLayerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private MapService Maps => _database.GetService<MapService>();

    [Fact]
    public async Task SavesLayersAndAssignsMarkers()
    {
        var context = await Maps.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Weltkarte";

        var layer = new MapLayer { MapId = context.Entity.Id, Name = "NPCs", IsVisible = false };
        context.Entity.Layers.Add(layer);
        context.Entity.Markers.Add(new MapMarker { MapId = context.Entity.Id, X = 0.5, Y = 0.5, LayerId = layer.Id });

        await Maps.SaveMapAsync(context);

        var reloaded = await Maps.LoadForEditAsync(_database.ProjectId, context.Entity.Id);
        var storedLayer = Assert.Single(reloaded!.Entity.Layers);
        Assert.Equal("NPCs", storedLayer.Name);
        Assert.False(storedLayer.IsVisible);
        Assert.Equal(storedLayer.Id, Assert.Single(reloaded.Entity.Markers).LayerId);
    }

    [Fact]
    public async Task MarkerOfRemovedLayerFallsBackToBaseLayer()
    {
        var context = await Maps.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Höhle";

        var layer = new MapLayer { MapId = context.Entity.Id, Name = "Notizen" };
        context.Entity.Layers.Add(layer);
        context.Entity.Markers.Add(new MapMarker { MapId = context.Entity.Id, X = 0.2, Y = 0.3, LayerId = layer.Id });
        await Maps.SaveMapAsync(context);

        // Ebene entfernen, Markierung behalten — sie gehört danach zur Grundebene.
        var editing = await Maps.LoadForEditAsync(_database.ProjectId, context.Entity.Id);
        editing!.Entity.Layers.Clear();
        await Maps.SaveMapAsync(editing);

        var reloaded = await Maps.LoadForEditAsync(_database.ProjectId, context.Entity.Id);
        Assert.Empty(reloaded!.Entity.Layers);
        Assert.Null(Assert.Single(reloaded.Entity.Markers).LayerId);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.MapLayers.ToListAsync());
    }

    [Fact]
    public async Task LayerNeedsAName()
    {
        var context = await Maps.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Dorf";
        context.Entity.Layers.Add(new MapLayer { MapId = context.Entity.Id, Name = " " });

        await Assert.ThrowsAsync<ContentValidationException>(() => Maps.SaveMapAsync(context));
    }
}
