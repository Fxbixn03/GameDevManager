using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile der Whiteboard-Übersicht.</summary>
public sealed record WhiteboardRow(
    Guid Id,
    string Name,
    int NoteCount,
    int StrokeCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Meldet gespeicherte Whiteboard-Änderungen an alle offenen Ansichten — so sehen mehrere
/// Nutzer, die gleichzeitig am selben Board arbeiten, die Striche und Notizen der anderen.
/// Singleton, weil die Blazor-Verbindungen der Nutzer getrennte Scopes sind; das Ereignis
/// trägt eine Absender-Marke, damit die auslösende Ansicht nicht sich selbst neu lädt.
/// </summary>
public class WhiteboardNotifier
{
    public event Action<Guid, object?>? Changed;

    public void NotifyChanged(Guid whiteboardId, object? origin) =>
        Changed?.Invoke(whiteboardId, origin);
}

/// <summary>
/// Lesen und Schreiben der Whiteboards. Werkzeug-Daten ohne Änderungsprotokoll — ein
/// Pinselstrich ist keine Änderung am Spielinhalt.
/// </summary>
public class WhiteboardService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    WhiteboardNotifier notifier,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    public async Task<List<WhiteboardRow>> GetBoardsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Whiteboards
            .AsNoTracking()
            .Where(w => w.GameProjectId == projectId)
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
            .Select(w => new WhiteboardRow(w.Id, w.Name, w.Notes.Count, w.Strokes.Count, w.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<Whiteboard?> GetBoardAsync(Guid projectId, Guid boardId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Whiteboards
            .AsNoTracking()
            .Include(w => w.Notes)
            .Include(w => w.Strokes)
            .FirstOrDefaultAsync(w => w.Id == boardId && w.GameProjectId == projectId, ct);
    }

    public async Task<Whiteboard> CreateBoardAsync(Guid projectId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["WhiteboardNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var maxOrder = await db.Whiteboards
            .Where(w => w.GameProjectId == projectId)
            .Select(w => (int?)w.SortOrder)
            .MaxAsync(ct) ?? -1;

        var board = new Whiteboard { GameProjectId = projectId, Name = name.Trim(), SortOrder = maxOrder + 1 };

        db.Whiteboards.Add(board);
        await db.SaveChangesAsync(ct);

        return board;
    }

    public async Task RenameBoardAsync(Guid boardId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["WhiteboardNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var board = await db.Whiteboards.FirstOrDefaultAsync(w => w.Id == boardId, ct);

        if (board is not null)
        {
            board.Name = name.Trim();
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        // Reines ExecuteDelete ohne vorheriges Speichern — hier greift der
        // WriteGuardInterceptor nicht, die Prüfung steht deshalb ausdrücklich da.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Notizen und Striche fallen über den Fremdschlüssel mit.
        await db.Whiteboards.Where(w => w.Id == boardId).ExecuteDeleteAsync(ct);
    }

    // ----------------------------------------------------------------------- Inhalte

    public async Task AddStrokeAsync(Guid boardId, WhiteboardStroke stroke, object? origin, CancellationToken ct = default)
    {
        if (WhiteboardStroke.ParsePoints(stroke.Points).Count < 2)
        {
            // Ein einzelner Punkt ist ein verirrter Klick, kein Strich.
            return;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        db.WhiteboardStrokes.Add(new WhiteboardStroke
        {
            Id = stroke.Id,
            WhiteboardId = boardId,
            Points = stroke.Points,
            Color = stroke.Color,
            Width = Math.Clamp(stroke.Width, 1, 30)
        });

        await db.SaveChangesAsync(ct);
        notifier.NotifyChanged(boardId, origin);
    }

    public async Task DeleteStrokeAsync(Guid strokeId, object? origin, CancellationToken ct = default)
    {
        // Reines ExecuteDelete — Prüfung ausdrücklich hier, siehe DeleteBoardAsync.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var boardId = await db.WhiteboardStrokes
            .Where(s => s.Id == strokeId)
            .Select(s => (Guid?)s.WhiteboardId)
            .FirstOrDefaultAsync(ct);

        await db.WhiteboardStrokes.Where(s => s.Id == strokeId).ExecuteDeleteAsync(ct);

        if (boardId is { } id)
        {
            notifier.NotifyChanged(id, origin);
        }
    }

    public async Task SaveNoteAsync(Guid boardId, WhiteboardNote note, object? origin, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.WhiteboardNotes.FirstOrDefaultAsync(n => n.Id == note.Id, ct);

        if (stored is null)
        {
            stored = new WhiteboardNote { Id = note.Id, WhiteboardId = boardId };
            db.WhiteboardNotes.Add(stored);
        }

        stored.X = Math.Clamp(note.X, 0, Whiteboard.CanvasWidth);
        stored.Y = Math.Clamp(note.Y, 0, Whiteboard.CanvasHeight);
        stored.Text = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim();
        stored.Color = string.IsNullOrWhiteSpace(note.Color) ? null : note.Color.Trim();

        await db.SaveChangesAsync(ct);
        notifier.NotifyChanged(boardId, origin);
    }

    public async Task DeleteNoteAsync(Guid noteId, object? origin, CancellationToken ct = default)
    {
        // Reines ExecuteDelete — Prüfung ausdrücklich hier, siehe DeleteBoardAsync.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var boardId = await db.WhiteboardNotes
            .Where(n => n.Id == noteId)
            .Select(n => (Guid?)n.WhiteboardId)
            .FirstOrDefaultAsync(ct);

        await db.WhiteboardNotes.Where(n => n.Id == noteId).ExecuteDeleteAsync(ct);

        if (boardId is { } id)
        {
            notifier.NotifyChanged(id, origin);
        }
    }
}
