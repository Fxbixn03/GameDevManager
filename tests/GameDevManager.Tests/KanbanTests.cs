using Xunit;
using GameDevManager.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Tests;

/// <summary>Kanban-Boards des ToDo-Moduls: Spalten, Karten, Verschieben, Berechtigungen.</summary>
public sealed class KanbanTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private KanbanService Kanban => _database.GetService<KanbanService>();

    [Fact]
    public async Task NewBoardStartsWithThreeColumns()
    {
        var board = await Kanban.CreateBoardAsync(_database.ProjectId, "Programmierung");

        var loaded = await Kanban.GetBoardAsync(_database.ProjectId, board.Id);
        Assert.Equal(3, loaded!.Columns.Count);
        Assert.Equal(["Offen", "In Arbeit", "Fertig"], loaded.Columns.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task MovesCardAcrossColumnsToPosition()
    {
        var board = await Kanban.CreateBoardAsync(_database.ProjectId, "Art");
        var loaded = await Kanban.GetBoardAsync(_database.ProjectId, board.Id);
        var open = loaded!.Columns[0];
        var doing = loaded.Columns[1];

        var a = await Kanban.AddCardAsync(open.Id, "Konzept");
        var b = await Kanban.AddCardAsync(doing.Id, "Sprite-Satz");
        var c = await Kanban.AddCardAsync(doing.Id, "Tileset");

        // „Konzept“ zwischen die beiden Karten der Spalte „In Arbeit“ schieben.
        await Kanban.MoveCardAsync(a.Id, doing.Id, 1);

        var after = await Kanban.GetBoardAsync(_database.ProjectId, board.Id);
        Assert.Empty(after!.Columns[0].Cards);
        Assert.Equal(
            [b.Id, a.Id, c.Id],
            after.Columns[1].Cards.Select(card => card.Id).ToArray());
    }

    [Fact]
    public async Task DeletingColumnTakesItsCards()
    {
        var board = await Kanban.CreateBoardAsync(_database.ProjectId, "Release");
        var loaded = await Kanban.GetBoardAsync(_database.ProjectId, board.Id);
        var column = loaded!.Columns[0];
        await Kanban.AddCardAsync(column.Id, "Trailer schneiden");

        await Kanban.DeleteColumnAsync(column.Id);

        await using var db = _database.CreateContext();
        Assert.Empty(await db.KanbanCards.ToListAsync());
        Assert.Equal(2, await db.KanbanColumns.CountAsync());
    }

    [Fact]
    public async Task ReadOnlyAccountCannotDeleteBoard()
    {
        var board = await Kanban.CreateBoardAsync(_database.ProjectId, "Planung");

        _database.Permissions.Current = new UserPermissions(false, false, true, true, null);

        await Assert.ThrowsAsync<ContentValidationException>(() => Kanban.DeleteBoardAsync(board.Id));
        await Assert.ThrowsAsync<ContentValidationException>(() => Kanban.CreateBoardAsync(_database.ProjectId, "Zweites"));
    }
}
