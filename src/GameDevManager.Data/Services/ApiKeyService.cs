using System.Security.Cryptography;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Ein frisch angelegter Schlüssel — der Klartext steht hier zum einzigen Mal.</summary>
public sealed record CreatedApiKey(ApiKey Key, string PlainText);

/// <summary>
/// Die Schlüssel der lesenden HTTP-API: anlegen, auflisten, sperren, prüfen.
/// <para>
/// Verwaltet wird nur mit <b>Verwalterrecht</b> — ein Schlüssel öffnet den Bestand für alles,
/// was ihn kennt, und wer das vergeben darf, soll derselbe sein, der auch Konten anlegt.
/// </para>
/// </summary>
public class ApiKeyService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Woran ein Schlüssel zu erkennen ist. Das Präfix steht auch in der Datenbank und macht
    /// ihn in einer Liste wiedererkennbar, ohne ihn zu verraten.
    /// </summary>
    public const string Prefix = "gdm_";

    /// <summary>Wie viele Zeichen des Klartexts als Erkennungszeichen gespeichert werden.</summary>
    private const int PrefixLength = 12;

    public async Task<List<ApiKey>> GetKeysAsync(CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ApiKeys
            .AsNoTracking()
            .Include(key => key.GameProject)
            .OrderByDescending(key => key.CreatedAtUtc)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Legt einen Schlüssel an und gibt ihn <b>einmal</b> im Klartext zurück. Danach steht in
    /// der Datenbank nur noch sein Hash — ein zweites Anzeigen gibt es nicht.
    /// </summary>
    public async Task<CreatedApiKey> CreateAsync(
        string name, Guid? projectId, DateTime? expiresAtUtc, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentValidationException(messages["ApiKey_NameRequired"].Value);
        }

        // 32 zufällige Bytes, URL-sicher kodiert — lang genug, dass Raten ausscheidet, und
        // ohne Zeichen, die in einem Header oder einer .env-Datei Ärger machen.
        var plain = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var key = new ApiKey
        {
            Name = name.Trim(),
            Prefix = plain[..PrefixLength],
            KeyHash = PasswordHasher.Hash(plain),
            GameProjectId = projectId,
            ExpiresAtUtc = expiresAtUtc
        };

        await using var db = await factory.CreateDbContextAsync(ct);

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);

        return new CreatedApiKey(key, plain);
    }

    /// <summary>Sperrt einen Schlüssel oder gibt ihn wieder frei.</summary>
    public async Task SetDisabledAsync(Guid keyId, bool disabled, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var key = await db.ApiKeys.FirstOrDefaultAsync(entry => entry.Id == keyId, ct);
        if (key is null)
        {
            return;
        }

        key.IsDisabled = disabled;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid keyId, CancellationToken ct = default)
    {
        await guard.EnsureAdministratorAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.ApiKeys.Where(key => key.Id == keyId).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Prüft einen mitgeschickten Schlüssel und gibt ihn zurück, wenn er gilt — sonst
    /// <c>null</c>. Ohne Rechteprüfung: Diese Methode <b>ist</b> die Prüfung, und sie läuft,
    /// bevor irgendjemand angemeldet ist.
    /// <para>
    /// Vorausgewählt wird über das Präfix, verglichen wird über den Hash. Ohne die Vorauswahl
    /// müsste jede Anfrage jeden Schlüssel der Installation durchrechnen — PBKDF2 ist
    /// absichtlich langsam.
    /// </para>
    /// </summary>
    public async Task<ApiKey?> ValidateAsync(string? plainText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainText) || plainText.Length <= PrefixLength)
        {
            return null;
        }

        var prefix = plainText[..PrefixLength];

        await using var db = await factory.CreateDbContextAsync(ct);

        var candidates = await db.ApiKeys
            .Where(key => key.Prefix == prefix)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(key => PasswordHasher.Verify(plainText, key.KeyHash));

        if (match is null || !match.IsValidNow)
        {
            return null;
        }

        // Der Zeitstempel beantwortet später „wird der noch benutzt?“. Auf die Minute genau
        // wäre eine Schreiboperation je Anfrage — einmal pro Stunde reicht dafür.
        if (match.LastUsedAtUtc is not { } last || DateTime.UtcNow - last > TimeSpan.FromHours(1))
        {
            match.LastUsedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return match;
    }
}
