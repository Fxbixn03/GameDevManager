using System.Security.Cryptography;

namespace GameDevManager.Data.Services;

/// <summary>
/// Passwörter als PBKDF2-HMAC-SHA256-Hash.
/// <para>
/// Bewusst selbst gebaut statt über das ASP.NET-Identity-Paket: Gebraucht wird genau eine
/// Sache — ein Passwort prüfen —, und dafür das ganze Identity-Modell samt seiner sieben
/// Tabellen in alle vier Provider zu migrieren wäre ein Vielfaches an Umfang für dasselbe
/// Ergebnis. Dieselbe Abwägung wie beim <c>ImageDimensionReader</c>, der die Bildmaße aus dem
/// Dateikopf liest, statt eine Bildbibliothek mitzubringen.
/// </para>
/// <para>
/// Das Format ist <c>pbkdf2-sha256$&lt;Runden&gt;$&lt;Salz&gt;$&lt;Hash&gt;</c>, beides
/// Base64. Die Rundenzahl steht im Hash und nicht im Code: Wird sie später erhöht, lassen sich
/// alte Passwörter weiterhin prüfen — geprüft wird mit den Runden, mit denen gehasht wurde.
/// </para>
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Rundenzahl für neue Passwörter (OWASP-Empfehlung für PBKDF2-HMAC-SHA256).</summary>
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Prüft ein Passwort gegen einen gespeicherten Hash. Ein unlesbarer Hash ergibt
    /// <c>false</c> und keine Ausnahme — ein beschädigter Datensatz soll die Anmeldeseite
    /// nicht zerlegen, sondern den Benutzer nicht hereinlassen.
    /// </summary>
    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');

        if (parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Zeitkonstant vergleichen: Ein Vergleich, der beim ersten falschen Byte abbricht,
        // verrät über die Dauer, wie viel schon stimmte.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
