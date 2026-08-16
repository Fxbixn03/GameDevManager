using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben der Spielwährungen samt ihrer benutzerdefinierten Feldwerte.
/// </summary>
public class CurrencyService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Übersicht aller Währungen eines Projekts, alphabetisch.</summary>
    public async Task<List<CurrencyListRow>> GetCurrenciesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Currencies
            .AsNoTracking()
            .Where(c => c.GameProjectId == projectId)
            .OrderBy(c => c.Name)
            .Select(c => new CurrencyListRow(
                c.Id,
                c.Name,
                c.Symbol,
                c.Description,
                c.ContentTypeId,
                c.ContentType!.Name,
                c.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == c.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Currency>?> LoadForEditAsync(
        Guid projectId, Guid? currencyId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Currencies, ct);

        if (currencyId is null)
        {
            return new ContentEditContext<Currency>
            {
                Entity = new Currency { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var currency = await db.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == currencyId && c.GameProjectId == projectId, ct);

        if (currency is null)
        {
            return null;
        }

        return new ContentEditContext<Currency>
        {
            Entity = currency,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, currency.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, currency.Id, ct)
        };
    }

    public async Task SaveCurrencyAsync(ContentEditContext<Currency> context, CancellationToken ct = default)
    {
        var currency = context.Entity;

        if (string.IsNullOrWhiteSpace(currency.Name))
        {
            throw new ContentValidationException(messages["CurrencyNameRequired"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        // Zwei Währungen mit demselben Namen wären in jeder Preisangabe nicht auseinanderzuhalten.
        var name = currency.Name.Trim();
        var taken = await db.Currencies.AnyAsync(
            other => other.GameProjectId == currency.GameProjectId
                && other.Name == name
                && other.Id != currency.Id, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["CurrencyNameExists", name]);
        }

        var now = DateTime.UtcNow;
        var stored = await db.Currencies.FirstOrDefaultAsync(c => c.Id == currency.Id, ct);

        if (stored is null)
        {
            stored = new Currency
            {
                Id = currency.Id,
                GameProjectId = currency.GameProjectId,
                Name = name,
                CreatedAtUtc = now
            };

            db.Currencies.Add(stored);
        }

        stored.ContentTypeId = currency.ContentTypeId;
        stored.Name = name;
        stored.Symbol = Normalize(currency.Symbol);
        stored.Description = Normalize(currency.Description);
        // Ein Kurs von 0 oder darunter machte jede Umrechnung sinnlos.
        stored.ExchangeRate = currency.ExchangeRate > 0 ? currency.ExchangeRate : 1;
        stored.UpdatedAtUtc = now;

        // Der Bearbeitungsstand hängt an der Basis aller Inhalte und wird deshalb hier
        // gesetzt und nicht in jedem Zweig der Fallunterscheidung darüber.
        stored.Status = context.Entity.Status;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        currency.CreatedAtUtc = stored.CreatedAtUtc;
        currency.UpdatedAtUtc = stored.UpdatedAtUtc;
        currency.Name = stored.Name;
        currency.Symbol = stored.Symbol;
        currency.Description = stored.Description;
    }

    /// <summary>Löscht eine Währung mit ihren Werten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteCurrencyAsync(Guid currencyId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(currencyId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Currencies, currencyId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, currencyId, ct);

        await db.Currencies
            .Where(c => c.Id == currencyId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
