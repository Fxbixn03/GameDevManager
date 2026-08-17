using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Weltzustände — Tageszeiten, Wetterlagen und Biome.
/// Die Feldmechanik kommt wie überall aus <see cref="ContentFields"/>.
/// </summary>
public class WorldService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Übersicht aller Weltzustände eines Projekts: nach Ausprägung gruppiert, innerhalb
    /// davon in der eingestellten Reihenfolge.
    /// </summary>
    public async Task<List<WorldStateListRow>> GetStatesAsync(
        Guid projectId, WorldStateKind? kind = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.WorldStates
            .AsNoTracking()
            .Where(w => w.GameProjectId == projectId && (kind == null || w.Kind == kind))
            .OrderBy(w => w.Kind)
            .ThenBy(w => w.SortOrder)
            .ThenBy(w => w.Name)
            .Select(w => new WorldStateListRow(
                w.Id,
                w.Name,
                w.Description,
                w.Kind,
                w.SortOrder,
                w.Color,
                w.ContentTypeId,
                w.ContentType!.Name,
                db.Conditions.Count(c => c.TargetEntityId == w.Id),
                w.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == w.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<WorldState>?> LoadForEditAsync(
        Guid projectId, Guid? stateId, WorldStateKind kind = WorldStateKind.TimeOfDay,
        CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.World, ct);

        if (stateId is null)
        {
            await using var fresh = await factory.CreateDbContextAsync(ct);

            // Neue Zustände reihen sich hinten in ihre Ausprägung ein — eine neue Tageszeit
            // gehört ans Ende der Abfolge, nicht an den Anfang.
            var nextOrder = await fresh.WorldStates
                .CountAsync(w => w.GameProjectId == projectId && w.Kind == kind, ct);

            return new ContentEditContext<WorldState>
            {
                Entity = new WorldState
                {
                    GameProjectId = projectId,
                    Name = string.Empty,
                    Kind = kind,
                    SortOrder = nextOrder
                },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var state = await db.WorldStates
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == stateId && w.GameProjectId == projectId, ct);

        if (state is null)
        {
            return null;
        }

        return new ContentEditContext<WorldState>
        {
            Entity = state,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, state.Id, ct),
            Values = await ContentFields.LoadValuesAsync<WorldState>(db, state.Id, ct)
        };
    }

    public async Task SaveStateAsync(ContentEditContext<WorldState> context, CancellationToken ct = default)
    {
        var state = context.Entity;

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            throw new ContentValidationException(messages["WorldStateNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Eindeutig je Ausprägung und nicht projektweit: „Klar“ kann eine Wetterlage und ein
        // Biom-Merkmal sein, aber zwei Wetterlagen „Klar“ wären in jeder Bedingung dieselbe.
        var name = state.Name.Trim();
        var taken = await db.WorldStates.AnyAsync(
            other => other.GameProjectId == state.GameProjectId
                && other.Kind == state.Kind
                && other.Name == name
                && other.Id != state.Id, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["WorldStateNameExists", name]);
        }

        var now = DateTime.UtcNow;
        var stored = await db.WorldStates.FirstOrDefaultAsync(w => w.Id == state.Id, ct);

        if (stored is null)
        {
            stored = new WorldState
            {
                Id = state.Id,
                GameProjectId = state.GameProjectId,
                Name = name,
                CreatedAtUtc = now
            };

            db.WorldStates.Add(stored);
        }

        stored.ContentTypeId = state.ContentTypeId;
        stored.Name = name;
        stored.Kind = state.Kind;
        stored.SortOrder = state.SortOrder;
        stored.Color = Normalize(state.Color);
        stored.Description = Normalize(state.Description);
        stored.UpdatedAtUtc = now;

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        state.CreatedAtUtc = stored.CreatedAtUtc;
        state.UpdatedAtUtc = stored.UpdatedAtUtc;
        state.Name = stored.Name;
        state.Color = stored.Color;
        state.Description = stored.Description;
    }

    /// <summary>Verschiebt einen Zustand innerhalb seiner Ausprägung um eine Stelle.</summary>
    public async Task MoveAsync(Guid stateId, int direction, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var state = await db.WorldStates.FirstOrDefaultAsync(w => w.Id == stateId, ct);
        if (state is null)
        {
            return;
        }

        var siblings = await db.WorldStates
            .Where(w => w.GameProjectId == state.GameProjectId && w.Kind == state.Kind)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Name)
            .ToListAsync(ct);

        var index = siblings.FindIndex(w => w.Id == stateId);
        var target = index + direction;

        if (index < 0 || target < 0 || target >= siblings.Count)
        {
            return;
        }

        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);

        var now = DateTime.UtcNow;

        for (var position = 0; position < siblings.Count; position++)
        {
            if (siblings[position].SortOrder != position)
            {
                siblings[position].SortOrder = position;
                siblings[position].UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht einen Weltzustand mit seinen Werten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteStateAsync(Guid stateId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(stateId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.WorldStates, stateId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, db.WorldStates, stateId, null, ct);

        await db.WorldStates
            .Where(w => w.Id == stateId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
