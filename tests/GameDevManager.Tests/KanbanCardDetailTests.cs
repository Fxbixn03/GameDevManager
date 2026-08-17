using GameDevManager.Data.Services;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die erweiterten Kanban-Karten: Zuständiger, Fälligkeit, Marke und die Verknüpfung auf eine
/// Entität. Der interessante Teil ist der Gegenzug — was an einer Entität noch aussteht.
/// </summary>
public class KanbanCardDetailTests
{
    private static async Task<(KanbanBoard Board, KanbanCard Card)> SeedAsync(TestDatabase test)
    {
        var kanban = test.GetService<KanbanService>();

        var board = await kanban.CreateBoardAsync(test.ProjectId, "Produktion");
        var loaded = await kanban.GetBoardAsync(test.ProjectId, board.Id);
        var card = await kanban.AddCardAsync(loaded!.Columns[0].Id, "Schaden prüfen");

        return (loaded, card);
    }

    private static async Task<Guid> SeedItemAsync(TestDatabase test, string name = "Schwert")
    {
        await using var db = test.CreateContext();

        var item = new Item { GameProjectId = test.ProjectId, Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    private static async Task<Guid> SeedUserAsync(TestDatabase test, string name)
    {
        await using var db = test.CreateContext();

        var user = new AppUser { UserName = name, DisplayName = name, PasswordHash = string.Empty };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    [Fact]
    public async Task Zustaendiger_Faelligkeit_und_Marke_werden_gespeichert()
    {
        using var test = new TestDatabase();
        var (board, card) = await SeedAsync(test);
        var userId = await SeedUserAsync(test, "Alrik");

        card.AssignedUserId = userId;
        card.DueDate = new DateTime(2026, 5, 1, 14, 30, 0);
        card.Label = "  Balancing  ";
        card.Color = "#FFC300";

        var kanban = test.GetService<KanbanService>();
        await kanban.UpdateCardAsync(card);

        var stored = (await kanban.GetBoardAsync(test.ProjectId, board.Id))!.Columns[0].Cards[0];

        Assert.Equal(userId, stored.AssignedUserId);
        Assert.Equal("Balancing", stored.Label);
        Assert.Equal("#FFC300", stored.Color);

        // Eine Aufgabe ist an einem Tag fällig, nicht um 14:30.
        Assert.Equal(new DateTime(2026, 5, 1), stored.DueDate);
    }

    [Fact]
    public async Task Ein_Ziel_ohne_Modul_wird_nicht_gespeichert()
    {
        using var test = new TestDatabase();
        var (board, card) = await SeedAsync(test);

        card.TargetModuleKey = ModuleKeys.Items;
        card.TargetEntityId = null;

        var kanban = test.GetService<KanbanService>();
        await kanban.UpdateCardAsync(card);

        // Beides gilt nur zusammen — ein Modul ohne Ziel wäre keine Verknüpfung.
        Assert.Null((await kanban.GetBoardAsync(test.ProjectId, board.Id))!.Columns[0].Cards[0].TargetModuleKey);
    }

    [Fact]
    public async Task Die_Entitaet_kennt_ihre_offenen_Aufgaben()
    {
        using var test = new TestDatabase();
        var (board, card) = await SeedAsync(test);
        var itemId = await SeedItemAsync(test);

        card.TargetModuleKey = ModuleKeys.Items;
        card.TargetEntityId = itemId;

        var kanban = test.GetService<KanbanService>();
        await kanban.UpdateCardAsync(card);

        var task = Assert.Single(await kanban.GetCardsForEntityAsync(itemId));

        Assert.Equal("Schaden prüfen", task.Title);
        Assert.Equal(board.Id, task.BoardId);
        Assert.False(task.IsDone);
    }

    [Fact]
    public async Task In_der_letzten_Spalte_gilt_eine_Aufgabe_als_erledigt()
    {
        using var test = new TestDatabase();
        var (board, card) = await SeedAsync(test);
        var itemId = await SeedItemAsync(test);

        var kanban = test.GetService<KanbanService>();

        card.TargetModuleKey = ModuleKeys.Items;
        card.TargetEntityId = itemId;
        await kanban.UpdateCardAsync(card);

        // Beim Kanban ist die Spalte der Zustand — ein eigener Schalter könnte davon abweichen.
        var last = (await kanban.GetBoardAsync(test.ProjectId, board.Id))!.Columns[^1];
        await kanban.MoveCardAsync(card.Id, last.Id, 0);

        Assert.True(Assert.Single(await kanban.GetCardsForEntityAsync(itemId)).IsDone);
    }

    [Fact]
    public async Task Meine_Aufgaben_zeigen_offene_Karten_Faelliges_zuerst()
    {
        using var test = new TestDatabase();
        var (board, first) = await SeedAsync(test);
        var userId = await SeedUserAsync(test, "Alrik");
        var otherId = await SeedUserAsync(test, "Brida");

        var kanban = test.GetService<KanbanService>();
        var columns = (await kanban.GetBoardAsync(test.ProjectId, board.Id))!.Columns;

        first.AssignedUserId = userId;
        await kanban.UpdateCardAsync(first);

        var dated = await kanban.AddCardAsync(columns[0].Id, "Loot prüfen");
        dated.AssignedUserId = userId;
        dated.DueDate = new DateTime(2026, 5, 1);
        await kanban.UpdateCardAsync(dated);

        // Eine erledigte (letzte Spalte) und eine fremde Karte gehören nicht in die Liste.
        var done = await kanban.AddCardAsync(columns[^1].Id, "Schon erledigt");
        done.AssignedUserId = userId;
        await kanban.UpdateCardAsync(done);

        var foreign = await kanban.AddCardAsync(columns[0].Id, "Bridas Aufgabe");
        foreign.AssignedUserId = otherId;
        await kanban.UpdateCardAsync(foreign);

        test.Author.Current = new ChangeAuthor(userId, "Alrik");
        var tasks = await kanban.GetMyOpenCardsAsync(test.ProjectId);

        // Was einen Termin hat, drängt zuerst; ohne Fälligkeit ans Ende.
        Assert.Equal(["Loot prüfen", "Schaden prüfen"], tasks.Select(task => task.Title));
        Assert.Equal(board.Id, tasks[0].BoardId);
    }

    [Fact]
    public async Task Ohne_Anmeldung_gibt_es_keine_Aufgabenliste()
    {
        using var test = new TestDatabase();
        var (_, card) = await SeedAsync(test);
        var userId = await SeedUserAsync(test, "Alrik");

        var kanban = test.GetService<KanbanService>();
        card.AssignedUserId = userId;
        await kanban.UpdateCardAsync(card);

        test.Author.Current = new ChangeAuthor(null, "System");

        Assert.Empty(await kanban.GetMyOpenCardsAsync(test.ProjectId));
    }

    [Fact]
    public async Task Ein_geloeschtes_Konto_nimmt_die_Karte_nicht_mit()
    {
        using var test = new TestDatabase();
        var (board, card) = await SeedAsync(test);
        var userId = await SeedUserAsync(test, "Alrik");

        var kanban = test.GetService<KanbanService>();
        card.AssignedUserId = userId;
        await kanban.UpdateCardAsync(card);

        await using (var db = test.CreateContext())
        {
            await db.AppUsers.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }

        var stored = (await kanban.GetBoardAsync(test.ProjectId, board.Id))!.Columns[0].Cards[0];

        // Die Karte bleibt, nur ohne Zuständigen.
        Assert.Equal("Schaden prüfen", stored.Title);
        Assert.Null(stored.AssignedUserId);
    }

    [Fact]
    public async Task Gesperrte_Konten_stehen_nicht_zur_Auswahl()
    {
        using var test = new TestDatabase();
        await SeedUserAsync(test, "Alrik");
        var blockedId = await SeedUserAsync(test, "Brida");

        await using (var db = test.CreateContext())
        {
            var blocked = await db.AppUsers.FirstAsync(user => user.Id == blockedId);
            blocked.IsDisabled = true;
            await db.SaveChangesAsync();
        }

        var assignees = await test.GetService<KanbanService>().GetAssigneesAsync();

        Assert.Equal("Alrik", Assert.Single(assignees).DisplayName);
    }
}
