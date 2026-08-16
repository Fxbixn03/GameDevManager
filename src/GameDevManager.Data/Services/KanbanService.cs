using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile der Board-Übersicht.</summary>
/// <summary>
/// Eine Aufgabe, die an einer Entität hängt — samt Board, Spalte und Zuständigem, damit die
/// Bearbeitungsmaske sie ohne zweite Abfrage anzeigen kann.
/// </summary>
/// <summary>Ein Konto, dem sich eine Aufgabe zuweisen lässt — nur Name und GUID.</summary>
public sealed record TaskAssignee(Guid Id, string DisplayName);

public sealed record KanbanCardLink(
    Guid CardId,
    Guid BoardId,
    string BoardName,
    string ColumnName,
    string Title,
    string? AssignedTo,
    DateTime? DueDate,
    bool IsDone);

public sealed record KanbanBoardRow(
    Guid Id,
    string Name,
    string? Description,
    int ColumnCount,
    int CardCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Lesen und Schreiben der Kanban-Boards. Werkzeug-Daten ohne Änderungsprotokoll —
/// eine verschobene Karte ist keine Änderung am Spielinhalt.
/// </summary>
public class KanbanService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<KanbanBoardRow>> GetBoardsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.KanbanBoards
            .AsNoTracking()
            .Where(b => b.GameProjectId == projectId)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Name)
            .Select(b => new KanbanBoardRow(
                b.Id,
                b.Name,
                b.Description,
                b.Columns.Count,
                b.Columns.SelectMany(c => c.Cards).Count(),
                b.CreatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>Ein Board komplett, Spalten und Karten in ihrer Reihenfolge.</summary>
    public async Task<KanbanBoard?> GetBoardAsync(Guid projectId, Guid boardId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var board = await db.KanbanBoards
            .AsNoTracking()
            .Include(b => b.Columns).ThenInclude(c => c.Cards)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.GameProjectId == projectId, ct);

        if (board is null)
        {
            return null;
        }

        board.Columns = [.. board.Columns.OrderBy(c => c.SortOrder)];

        foreach (var column in board.Columns)
        {
            column.Cards = [.. column.Cards.OrderBy(c => c.SortOrder)];
        }

        return board;
    }

    /// <summary>Legt ein Board an — mit den drei üblichen Spalten als Startpunkt.</summary>
    public async Task<KanbanBoard> CreateBoardAsync(Guid projectId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["KanbanBoardNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var maxOrder = await db.KanbanBoards
            .Where(b => b.GameProjectId == projectId)
            .Select(b => (int?)b.SortOrder)
            .MaxAsync(ct) ?? -1;

        var board = new KanbanBoard
        {
            GameProjectId = projectId,
            Name = name.Trim(),
            SortOrder = maxOrder + 1
        };

        string[] defaults =
            [messages["KanbanDefaultColumnOpen"], messages["KanbanDefaultColumnDoing"], messages["KanbanDefaultColumnDone"]];

        for (var index = 0; index < defaults.Length; index++)
        {
            board.Columns.Add(new KanbanColumn { BoardId = board.Id, Name = defaults[index], SortOrder = index });
        }

        db.KanbanBoards.Add(board);
        await db.SaveChangesAsync(ct);

        return board;
    }

    public async Task RenameBoardAsync(Guid boardId, string name, string? description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["KanbanBoardNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var board = await db.KanbanBoards.FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null)
        {
            return;
        }

        board.Name = name.Trim();
        board.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete ohne vorheriges Speichern — hier greift der
        // WriteGuardInterceptor nicht, die Prüfung steht deshalb ausdrücklich da.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Spalten und Karten fallen über den Fremdschlüssel mit.
        await db.KanbanBoards.Where(b => b.Id == boardId).ExecuteDeleteAsync(ct);
    }

    // ---------------------------------------------------------------------- Spalten

    public async Task<KanbanColumn> AddColumnAsync(Guid boardId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["KanbanColumnNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var maxOrder = await db.KanbanColumns
            .Where(c => c.BoardId == boardId)
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(ct) ?? -1;

        var column = new KanbanColumn { BoardId = boardId, Name = name.Trim(), SortOrder = maxOrder + 1 };

        db.KanbanColumns.Add(column);
        await db.SaveChangesAsync(ct);

        return column;
    }

    public async Task RenameColumnAsync(Guid columnId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["KanbanColumnNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == columnId, ct);

        if (column is not null)
        {
            column.Name = name.Trim();
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Schiebt eine Spalte einen Platz nach links oder rechts.</summary>
    public async Task MoveColumnAsync(Guid columnId, bool left, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == columnId, ct);

        if (column is null)
        {
            return;
        }

        var columns = await db.KanbanColumns
            .Where(c => c.BoardId == column.BoardId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        for (var index = 0; index < columns.Count; index++)
        {
            columns[index].SortOrder = index;
        }

        var neighborOrder = left ? column.SortOrder - 1 : column.SortOrder + 1;
        var neighbor = columns.FirstOrDefault(c => c.SortOrder == neighborOrder);

        if (neighbor is not null)
        {
            (column.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, column.SortOrder);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteColumnAsync(Guid columnId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete — Prüfung ausdrücklich hier, siehe DeleteBoardAsync.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Die Karten fallen über den Fremdschlüssel mit.
        await db.KanbanColumns.Where(c => c.Id == columnId).ExecuteDeleteAsync(ct);
    }

    // ----------------------------------------------------------------------- Karten

    public async Task<KanbanCard> AddCardAsync(Guid columnId, string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ContentValidationException(messages["KanbanCardTitleRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var maxOrder = await db.KanbanCards
            .Where(c => c.ColumnId == columnId)
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(ct) ?? -1;

        var card = new KanbanCard { ColumnId = columnId, Title = title.Trim(), SortOrder = maxOrder + 1 };

        db.KanbanCards.Add(card);
        await db.SaveChangesAsync(ct);

        return card;
    }

    /// <summary>
    /// Schreibt eine Karte zurück. Übergeben wird die bearbeitete Karte selbst und keine
    /// wachsende Reihe von Einzelwerten — mit Zuständigem, Fälligkeit, Marke und Verknüpfung
    /// wären es sonst acht Parameter, und der nächste käme bestimmt.
    /// </summary>
    public async Task UpdateCardAsync(KanbanCard edited, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(edited.Title))
        {
            throw new ContentValidationException(messages["KanbanCardTitleRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var card = await db.KanbanCards.FirstOrDefaultAsync(c => c.Id == edited.Id, ct);

        if (card is null)
        {
            return;
        }

        card.Title = edited.Title.Trim();
        card.Notes = Normalize(edited.Notes);
        card.AssignedUserId = edited.AssignedUserId;
        card.DueDate = edited.DueDate?.Date;
        card.Color = Normalize(edited.Color);
        card.Label = Normalize(edited.Label);

        // Ein Ziel ohne Modul wäre nicht aufzulösen — beides gilt nur zusammen.
        card.TargetEntityId = edited.TargetEntityId;
        card.TargetModuleKey = edited.TargetEntityId is null ? null : Normalize(edited.TargetModuleKey);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Die offenen Aufgaben, die an einer Entität hängen — der Gegenzug zur Verknüpfung, und
    /// wertvoller als sie: In der Bearbeitungsmaske steht damit, was an diesem Inhalt noch
    /// aussteht.
    /// <para>
    /// „Offen“ heißt: nicht in der letzten Spalte ihres Boards. Ein eigener Erledigt-Schalter
    /// stünde neben der Spalte, in der die Karte liegt, und beide könnten auseinanderlaufen —
    /// beim Kanban ist die Spalte der Zustand.
    /// </para>
    /// </summary>
    public async Task<List<KanbanCardLink>> GetCardsForEntityAsync(
        Guid entityId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var cards = await db.KanbanCards
            .AsNoTracking()
            .Where(card => card.TargetEntityId == entityId)
            .Select(card => new
            {
                card.Id,
                card.Title,
                card.DueDate,
                card.ColumnId,
                BoardId = card.Column!.BoardId,
                BoardName = card.Column!.Board!.Name,
                ColumnName = card.Column!.Name,
                ColumnOrder = card.Column!.SortOrder,
                AssignedTo = card.AssignedUser != null ? card.AssignedUser.DisplayName : null,
                LastColumnOrder = db.KanbanColumns
                    .Where(column => column.BoardId == card.Column!.BoardId)
                    .Max(column => column.SortOrder)
            })
            .ToListAsync(ct);

        return
        [
            .. cards
                .Select(card => new KanbanCardLink(
                    card.Id,
                    card.BoardId,
                    card.BoardName,
                    card.ColumnName,
                    card.Title,
                    card.AssignedTo,
                    card.DueDate,
                    card.ColumnOrder >= card.LastColumnOrder))
                .OrderBy(card => card.IsDone)
                .ThenBy(card => card.DueDate ?? DateTime.MaxValue)
                .ThenBy(card => card.Title)
        ];
    }

    /// <summary>
    /// Die Konten, denen sich eine Aufgabe zuweisen lässt. Bewusst nicht über den
    /// <c>UserService</c>: Der verlangt für seine Liste das Verwalterrecht, und wer eine
    /// Aufgabe verteilt, muss dafür keine Benutzer verwalten dürfen — gesperrte Konten bleiben
    /// trotzdem draußen.
    /// </summary>
    public async Task<List<TaskAssignee>> GetAssigneesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.AppUsers
            .AsNoTracking()
            .Where(user => !user.IsDisabled)
            .OrderBy(user => user.DisplayName)
            .Select(user => new TaskAssignee(user.Id, user.DisplayName))
            .ToListAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Verschiebt eine Karte — in eine andere Spalte und/oder an eine andere Position.
    /// <paramref name="targetIndex"/> ist die Zielposition innerhalb der Zielspalte.
    /// </summary>
    public async Task MoveCardAsync(Guid cardId, Guid targetColumnId, int targetIndex, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var card = await db.KanbanCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);

        if (card is null)
        {
            return;
        }

        var targetCards = await db.KanbanCards
            .Where(c => c.ColumnId == targetColumnId && c.Id != cardId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        card.ColumnId = targetColumnId;
        targetCards.Insert(Math.Clamp(targetIndex, 0, targetCards.Count), card);

        for (var index = 0; index < targetCards.Count; index++)
        {
            targetCards[index].SortOrder = index;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCardAsync(Guid cardId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete — Prüfung ausdrücklich hier, siehe DeleteBoardAsync.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.KanbanCards.Where(c => c.Id == cardId).ExecuteDeleteAsync(ct);
    }
}
