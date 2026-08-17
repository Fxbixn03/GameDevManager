using System.Security.Cryptography;
using System.Text;

namespace GameDevManager.Data.Services;

/// <summary>
/// Einmalkennwörter nach RFC 6238 (TOTP) — der zweite Faktor der Anmeldung.
/// <para>
/// Selbst gerechnet und nicht als Fremdbibliothek: Es sind wenige Dutzend Zeilen über
/// HMAC-SHA1, und das Verfahren steht seit 2011 fest — dieselbe Abwägung wie beim
/// <c>ImageDimensionReader</c>, dem <c>Csv</c> und dem <c>CurveExpression</c>. Eine
/// Abhängigkeit, die sich ändern kann, wäre hier das größere Risiko als der Code.
/// </para>
/// <para>
/// Bewusst <b>HMAC-SHA1</b> und sechs Stellen: Das ist, was jede Authenticator-App ohne
/// Zusatzangabe erwartet. Ein stärkerer Hash wäre kryptografisch schöner und würde von der
/// Hälfte der Apps still falsch gerechnet.
/// </para>
/// </summary>
public static class Totp
{
    /// <summary>Die Schrittweite in Sekunden — der Standardwert, den jede App voraussetzt.</summary>
    public const int PeriodSeconds = 30;

    /// <summary>Stellen des Codes.</summary>
    private const int Digits = 6;

    /// <summary>
    /// Wie viele Schritte davor und danach noch gelten. Einer reicht: Er fängt eine Uhr ab,
    /// die um bis zu eine halbe Minute abweicht, ohne das Zeitfenster unnötig zu öffnen.
    /// </summary>
    private const int Tolerance = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Erzeugt ein neues Geheimnis als Base32 — 20 Byte, die Länge, für die RFC 4226 den
    /// HMAC-SHA1-Schlüssel vorsieht.
    /// </summary>
    public static string CreateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        var builder = new StringBuilder();

        // Base32 nach RFC 4648, ohne Auffüllzeichen: Authenticator-Apps lesen es so, und der
        // Nutzer kann es notfalls abtippen.
        for (var bitIndex = 0; bitIndex + 5 <= bytes.Length * 8; bitIndex += 5)
        {
            var value = 0;

            for (var offset = 0; offset < 5; offset++)
            {
                var bit = bitIndex + offset;
                value = (value << 1) | ((bytes[bit / 8] >> (7 - (bit % 8))) & 1);
            }

            builder.Append(Base32Alphabet[value]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Die <c>otpauth</c>-Adresse, die eine Authenticator-App als QR-Code liest — oder, hier,
    /// die man von Hand einträgt.
    /// </summary>
    public static string BuildUri(string issuer, string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
        + $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={PeriodSeconds}";

    /// <summary>
    /// Prüft einen eingegebenen Code gegen das Geheimnis. Der Schritt davor und danach gelten
    /// mit — eine Uhr, die eine halbe Minute abweicht, soll niemanden aussperren.
    /// </summary>
    public static bool Verify(string? secret, string? code, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var entered = new string([.. code.Where(char.IsAsciiDigit)]);

        if (entered.Length != Digits)
        {
            return false;
        }

        var key = FromBase32(secret);

        if (key.Length == 0)
        {
            return false;
        }

        var step = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PeriodSeconds;

        for (var offset = -Tolerance; offset <= Tolerance; offset++)
        {
            // Zeitkonstanter Vergleich: Ein Code ist ein Geheimnis, und die Laufzeit eines
            // Zeichenvergleichs verrät, wie viele Stellen stimmten.
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Compute(key, step + offset)),
                Encoding.ASCII.GetBytes(entered)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Der Code zu einem Zeitschritt — für Tests und die Bestätigung beim Einrichten.</summary>
    public static string Compute(string secret, DateTimeOffset moment) =>
        Compute(FromBase32(secret), moment.ToUnixTimeSeconds() / PeriodSeconds);

    private static string Compute(byte[] key, long step)
    {
        var counter = new byte[8];

        for (var index = 7; index >= 0; index--)
        {
            counter[index] = (byte)(step & 0xFF);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);

        // Dynamic Truncation nach RFC 4226: Die letzten vier Bits sagen, wo die vier Bytes
        // stehen, aus denen der Code entsteht.
        var start = hash[^1] & 0x0F;

        var binary = ((hash[start] & 0x7F) << 24)
            | ((hash[start + 1] & 0xFF) << 16)
            | ((hash[start + 2] & 0xFF) << 8)
            | (hash[start + 3] & 0xFF);

        return (binary % 1_000_000).ToString().PadLeft(Digits, '0');
    }

    private static byte[] FromBase32(string value)
    {
        var bits = 0;
        var buffer = 0;
        var bytes = new List<byte>();

        foreach (var character in value.ToUpperInvariant())
        {
            var index = Base32Alphabet.IndexOf(character);

            if (index < 0)
            {
                // Leerzeichen und Bindestriche kommen aus abgetippten Geheimnissen und sind
                // kein Fehler; alles andere macht das Geheimnis unbrauchbar.
                if (character is ' ' or '-' or '=')
                {
                    continue;
                }

                return [];
            }

            buffer = (buffer << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. bytes];
    }
}
