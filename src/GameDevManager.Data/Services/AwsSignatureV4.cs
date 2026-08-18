using System.Security.Cryptography;
using System.Text;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die AWS-Signatur Version 4 — der Handschlag, den jeder S3-kompatible Speicher versteht
/// (AWS, MinIO, Backblaze). Selbst gerechnet statt als SDK gezogen — dieselbe Abwägung wie
/// beim HMAC der Webhooks: Es sind zwei Hash-Ketten und eine Kopfzeile, und das SDK brächte
/// einen Abhängigkeitsbaum mit, den das Tool sonst nirgends braucht.
/// <para>
/// Gerechnet wird exakt nach Spezifikation: kanonische Anfrage → StringToSign →
/// Schlüsselkette → Signatur. Die Header müssen kleingeschrieben und sortiert hinein —
/// das übernimmt diese Klasse, damit kein Aufrufer daran denken muss. Geprüft gegen den
/// dokumentierten Testvektor von AWS (<c>AwsSignatureTests</c>).
/// </para>
/// </summary>
public static class AwsSignatureV4
{
    /// <summary>Der Payload-Hash für Ströme, die nicht zweimal gelesen werden sollen.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>SHA-256 als Hex — für Payloads und die kanonische Anfrage.</summary>
    public static string HashHex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// Baut den <c>Authorization</c>-Kopf. <paramref name="headers"/> sind die Header, die
    /// mitsigniert werden (mindestens <c>host</c>, <c>x-amz-content-sha256</c>,
    /// <c>x-amz-date</c>) — Reihenfolge egal, sortiert wird hier.
    /// </summary>
    public static string BuildAuthorization(
        string method,
        string canonicalUri,
        string canonicalQuery,
        IReadOnlyList<(string Name, string Value)> headers,
        string payloadHash,
        DateTime timestampUtc,
        string region,
        string accessKey,
        string secretKey,
        string service = "s3")
    {
        var sorted = headers
            .Select(header => (Name: header.Name.ToLowerInvariant(), Value: header.Value.Trim()))
            .OrderBy(header => header.Name, StringComparer.Ordinal)
            .ToList();

        var signedHeaders = string.Join(';', sorted.Select(header => header.Name));

        var canonicalRequest = string.Join('\n',
            method,
            canonicalUri,
            canonicalQuery,
            string.Concat(sorted.Select(header => $"{header.Name}:{header.Value}\n")),
            signedHeaders,
            payloadHash);

        var stamp = timestampUtc.ToString("yyyyMMdd'T'HHmmss'Z'");
        var date = timestampUtc.ToString("yyyyMMdd");
        var scope = $"{date}/{region}/{service}/aws4_request";

        var stringToSign = string.Join('\n',
            "AWS4-HMAC-SHA256",
            stamp,
            scope,
            HashHex(canonicalRequest));

        var signingKey = Hmac(Hmac(Hmac(Hmac(
            Encoding.UTF8.GetBytes("AWS4" + secretKey), date), region), service), "aws4_request");

        var signature = Convert.ToHexStringLower(Hmac(signingKey, stringToSign));

        return $"AWS4-HMAC-SHA256 Credential={accessKey}/{scope}, "
            + $"SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static byte[] Hmac(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
}
