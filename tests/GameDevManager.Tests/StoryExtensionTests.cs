using Xunit;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>
/// Die erweiterten Story-Abschnitte: Stimmung/Datum/Dauer/Ort, Karten-Verknüpfung,
/// Szenen-Verknüpfungen und die Drag-&amp;-Drop-Reihenfolge.
/// </summary>
public sealed class StoryExtensionTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private StoryService Story => _database.GetService<StoryService>();

    private MapService Maps => _database.GetService<MapService>();

    private async Task<StoryEntry> CreateEntryAsync(string name)
    {
        var context = await Story.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = name;
        await Story.SaveEntryAsync(context);
        return context.Entity;
    }

    [Fact]
    public async Task SavesSceneOptionsAndLinks()
    {
        var other = await CreateEntryAsync("Prolog");

        var mapContext = await Maps.LoadForEditAsync(_database.ProjectId, null);
        mapContext!.Entity.Name = "Hafen";
        await Maps.SaveMapAsync(mapContext);

        var context = await Story.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Sturmnacht";
        context.Entity.Mood = " bedrückend ";
        context.Entity.GameDate = "3. Tag der Aschewoche";
        context.Entity.Duration = "eine Nacht";
        context.Entity.Location = "Hafenviertel";
        context.Entity.TargetMapId = mapContext.Entity.Id;
        context.Entity.Links.Add(new StoryLink
        {
            StoryEntryId = context.Entity.Id,
            TargetEntryId = other.Id,
            Label = "Rückblende"
        });

        await Story.SaveEntryAsync(context);

        var reloaded = await Story.LoadForEditAsync(_database.ProjectId, context.Entity.Id);
        Assert.Equal("bedrückend", reloaded!.Entity.Mood);
        Assert.Equal("3. Tag der Aschewoche", reloaded.Entity.GameDate);
        Assert.Equal("eine Nacht", reloaded.Entity.Duration);
        Assert.Equal("Hafenviertel", reloaded.Entity.Location);
        Assert.Equal(mapContext.Entity.Id, reloaded.Entity.TargetMapId);

        var link = Assert.Single(reloaded.Entity.Links);
        Assert.Equal(other.Id, link.TargetEntryId);
        Assert.Equal("Rückblende", link.Label);
    }

    [Fact]
    public async Task RejectsSelfLink()
    {
        var entry = await CreateEntryAsync("Finale");

        var context = await Story.LoadForEditAsync(_database.ProjectId, entry.Id);
        context!.Entity.Links.Add(new StoryLink { StoryEntryId = entry.Id, TargetEntryId = entry.Id });

        await Assert.ThrowsAsync<ContentValidationException>(() => Story.SaveEntryAsync(context));
    }

    [Fact]
    public async Task DeletingTargetEntryRemovesIncomingLinks()
    {
        var target = await CreateEntryAsync("Prolog");
        var source = await CreateEntryAsync("Kapitel 1");

        var context = await Story.LoadForEditAsync(_database.ProjectId, source.Id);
        context!.Entity.Links.Add(new StoryLink { StoryEntryId = source.Id, TargetEntryId = target.Id });
        await Story.SaveEntryAsync(context);

        await Story.DeleteEntryAsync(target.Id);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.StoryLinks.ToListAsync());
    }

    [Fact]
    public async Task DeletingMapClearsSceneLocation()
    {
        var mapContext = await Maps.LoadForEditAsync(_database.ProjectId, null);
        mapContext!.Entity.Name = "Hafen";
        await Maps.SaveMapAsync(mapContext);

        var context = await Story.LoadForEditAsync(_database.ProjectId, null);
        context!.Entity.Name = "Sturmnacht";
        context.Entity.TargetMapId = mapContext.Entity.Id;
        await Story.SaveEntryAsync(context);

        await Maps.DeleteMapAsync(mapContext.Entity.Id);

        var reloaded = await Story.LoadForEditAsync(_database.ProjectId, context.Entity.Id);
        Assert.Null(reloaded!.Entity.TargetMapId);
        Assert.Null(reloaded.Entity.TargetMapMarkerId);
    }

    [Fact]
    public async Task ReorderAppliesDraggedOrder()
    {
        var first = await CreateEntryAsync("Eins");
        var second = await CreateEntryAsync("Zwei");
        var third = await CreateEntryAsync("Drei");

        await Story.ReorderAsync(_database.ProjectId, [third.Id, first.Id, second.Id]);

        var rows = await Story.GetEntriesAsync(_database.ProjectId);
        Assert.Equal(["Drei", "Eins", "Zwei"], rows.Select(r => r.Name).ToArray());
    }
}
