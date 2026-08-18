using System.Globalization;
using System.Xml.Linq;
using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Die S3-Konfiguration — <c>S3:*</c> in der <c>appsettings.Local.json</c>: Die Zugangsdaten
/// beschreiben die Installation und stehen in keinem Export und keiner Sicherung, wie die
/// Verbindungszeichenfolge. Ohne Endpoint, Bucket und Schlüssel ist die Spiegelung aus.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>Die Basisadresse, z. B. <c>https://s3.eu-central-1.amazonaws.com</c> oder der MinIO-Host.</summary>
    public string? Endpoint { get; set; }

    public string? Bucket { get; set; }

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>Die Signatur-Region. MinIO und Backblaze nehmen die Vorgabe an.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Der Wurzelordner im Bucket — darunter je Projekt ein Ordner.</summary>
    public string Prefix { get; set; } = "gamedevmanager";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>
/// Die S3-Spiegelung der Exportstände — Pfad-Stil (<c>endpoint/bucket/schlüssel</c>), damit
/// MinIO und Co. ohne Wildcard-DNS funktionieren; die Signatur rechnet
/// <see cref="AwsSignatureV4"/>, der Upload läuft mit <c>UNSIGNED-PAYLOAD</c>, damit das
/// ZIP nicht zweimal gelesen werden muss.
/// <para>
/// <b>Wirft nie</b>: Ein nicht erreichbares Sicherungsziel landet im Log und blockiert
/// weder Export noch Sicherheitsnetz — der Stand liegt trotzdem lokal.
/// </para>
/// </summary>
public sealed class S3SnapshotMirror(
    S3Options options,
    IHttpClientFactory clients,
    ILogger<S3SnapshotMirror> log) : ISnapshotMirror
{
    public bool IsConfigured => options.IsConfigured;

    public async Task UploadAsync(
        Guid projectId, string fileName, Stream content, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return;
        }

        try
        {
            using var request = Sign(
                HttpMethod.Put, KeyFor(projectId, fileName), string.Empty, AwsSignatureV4.UnsignedPayload);
            request.Content = new StreamContent(content);

            using var response = await clients.CreateClient(nameof(S3SnapshotMirror)).SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning(
                    "Die Spiegelung des Exportstands {FileName} ins Sicherungsziel schlug fehl (HTTP {Status}).",
                    fileName, (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "Die Spiegelung des Exportstands {FileName} ins Sicherungsziel schlug fehl.", fileName);
        }
    }

    public async Task<IReadOnlyList<RemoteSnapshot>> ListAsync(
        Guid projectId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return [];
        }

        try
        {
            // ListObjectsV2 mit dem Projekt-Ordner als Prefix. Die Query muss sortiert und
            // kanonisch in die Signatur — beide Parameter stehen deshalb ausgeschrieben da.
            var query = $"list-type=2&prefix={Uri.EscapeDataString($"{options.Prefix}/{projectId:N}/")}";

            using var request = Sign(HttpMethod.Get, "", query, AwsSignatureV4.HashHex(string.Empty));
            using var response = await clients.CreateClient(nameof(S3SnapshotMirror)).SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Das Sicherungsziel antwortet nicht (HTTP {Status}).", (int)response.StatusCode);
                return [];
            }

            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var ns = xml.Root!.Name.Namespace;

            return
            [
                .. xml.Root.Elements(ns + "Contents")
                    .Select(entry => new RemoteSnapshot(
                        Path.GetFileName(entry.Element(ns + "Key")!.Value),
                        long.TryParse(entry.Element(ns + "Size")?.Value, out var size) ? size : 0,
                        DateTime.TryParse(
                            entry.Element(ns + "LastModified")?.Value, CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal, out var modified)
                            ? modified
                            : DateTime.MinValue))
                    .OrderByDescending(snapshot => snapshot.FileName, StringComparer.Ordinal)
            ];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Das Sicherungsziel ließ sich nicht auflisten.");
            return [];
        }
    }

    /// <summary>Je Projekt ein Ordner; der Zeitstempel steckt im Dateinamen des Standes.</summary>
    private string KeyFor(Guid projectId, string fileName) =>
        $"{options.Prefix}/{projectId:N}/{fileName}";

    private HttpRequestMessage Sign(HttpMethod method, string key, string query, string payloadHash)
    {
        var endpoint = new Uri(options.Endpoint!.TrimEnd('/'));
        var canonicalUri = $"/{options.Bucket}" + (key.Length > 0 ? $"/{Uri.EscapeDataString(key).Replace("%2F", "/")}" : "");
        var now = DateTime.UtcNow;
        var stamp = now.ToString("yyyyMMdd'T'HHmmss'Z'");

        var request = new HttpRequestMessage(
            method, $"{endpoint.Scheme}://{endpoint.Authority}{canonicalUri}{(query.Length > 0 ? "?" + query : "")}");

        request.Headers.TryAddWithoutValidation("x-amz-date", stamp);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("Authorization", AwsSignatureV4.BuildAuthorization(
            method.Method,
            canonicalUri,
            query,
            [
                ("host", endpoint.Authority),
                ("x-amz-content-sha256", payloadHash),
                ("x-amz-date", stamp)
            ],
            payloadHash,
            now,
            options.Region,
            options.AccessKey!,
            options.SecretKey!));

        return request;
    }
}
