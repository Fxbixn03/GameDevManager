using Xunit;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>Whiteboards: Striche, Notizen und die Benachrichtigung offener Ansichten.</summary>
public sealed class WhiteboardTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private WhiteboardService Whiteboards => _database.GetService<WhiteboardService>();

    [Fact]
    public async Task SavesStrokesAndDropsStrayClicks()
    {
        var board = await Whiteboards.CreateBoardAsync(_database.ProjectId, "Skizzen");

        // Ein einzelner Punkt ist ein verirrter Klick und wird nicht gespeichert.
        await Whiteboards.AddStrokeAsync(board.Id, new WhiteboardStroke
        {
            WhiteboardId = board.Id,
            Points = "10,10"
        }, origin: null);

        await Whiteboards.AddStrokeAsync(board.Id, new WhiteboardStroke
        {
            WhiteboardId = board.Id,
            Points = "10,10;60,80;120,90",
            Color = "#FFC300",
            Width = 99
        }, origin: null);

        var loaded = await Whiteboards.GetBoardAsync(_database.ProjectId, board.Id);
        var stroke = Assert.Single(loaded!.Strokes);
        Assert.Equal(3, WhiteboardStroke.ParsePoints(stroke.Points).Count);
        // Die Strichstärke wird auf ein sinnvolles Maß begrenzt.
        Assert.Equal(30, stroke.Width);
    }

    [Fact]
    public async Task ClampsNotesToCanvasAndDeletes()
    {
        var board = await Whiteboards.CreateBoardAsync(_database.ProjectId, "Notizen");

        var note = new WhiteboardNote { WhiteboardId = board.Id, X = -50, Y = 99999, Text = " Idee " };
        await Whiteboards.SaveNoteAsync(board.Id, note, origin: null);

        var loaded = await Whiteboards.GetBoardAsync(_database.ProjectId, board.Id);
        var stored = Assert.Single(loaded!.Notes);
        Assert.Equal(0, stored.X);
        Assert.Equal(Whiteboard.CanvasHeight, stored.Y);
        Assert.Equal("Idee", stored.Text);

        await Whiteboards.DeleteNoteAsync(stored.Id, origin: null);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.WhiteboardNotes.ToListAsync());
    }

    [Fact]
    public async Task NotifierReportsChangesWithOrigin()
    {
        var notifier = _database.GetService<WhiteboardNotifier>();
        var board = await Whiteboards.CreateBoardAsync(_database.ProjectId, "Live");

        var received = new List<(Guid BoardId, object? Origin)>();
        notifier.Changed += (id, origin) => received.Add((id, origin));

        var me = new object();
        await Whiteboards.SaveNoteAsync(board.Id, new WhiteboardNote { WhiteboardId = board.Id, Text = "Hallo" }, me);

        var change = Assert.Single(received);
        Assert.Equal(board.Id, change.BoardId);
        // Die Absender-Marke kommt unverändert an — so lädt die auslösende Ansicht nicht selbst neu.
        Assert.Same(me, change.Origin);
    }
}
