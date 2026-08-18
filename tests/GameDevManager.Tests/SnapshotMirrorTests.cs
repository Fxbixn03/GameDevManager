using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die S3-Spiegelung: die von Hand gerechnete AWS-Signatur (gegen den dokumentierten
/// Testvektor) und die stille Vorgabe ohne Sicherungsziel.
/// </summary>
public class SnapshotMirrorTests
{
    /// <summary>
    /// Der Beispielfall aus der AWS-Dokumentation (SigV4, „GET Object“): examplebucket,
    /// test.txt, 24. Mai 2013 — die erwartete Signatur steht wörtlich in der Doku. Rechnet
    /// unsere Kette denselben Wert, stimmen kanonische Anfrage, StringToSign und
    /// Schlüsselableitung zusammen.
    /// </summary>
    [Fact]
    public void Die_Signatur_trifft_den_dokumentierten_AWS_Testvektor()
    {
        var emptyHash = AwsSignatureV4.HashHex(string.Empty);

        var authorization = AwsSignatureV4.BuildAuthorization(
            method: "GET",
            canonicalUri: "/test.txt",
            canonicalQuery: string.Empty,
            headers:
            [
                ("host", "examplebucket.s3.amazonaws.com"),
                ("range", "bytes=0-9"),
                ("x-amz-content-sha256", emptyHash),
                ("x-amz-date", "20130524T000000Z")
            ],
            payloadHash: emptyHash,
            timestampUtc: new DateTime(2013, 5, 24, 0, 0, 0, DateTimeKind.Utc),
            region: "us-east-1",
            accessKey: "AKIAIOSFODNN7EXAMPLE",
            secretKey: "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

        Assert.Contains(
            "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41",
            authorization);
        Assert.Contains("Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request", authorization);
        Assert.Contains("SignedHeaders=host;range;x-amz-content-sha256;x-amz-date", authorization);
    }

    [Fact]
    public async Task Ohne_Sicherungsziel_ist_die_Vorgabe_still()
    {
        using var test = new TestDatabase();

        var mirror = test.GetService<ISnapshotMirror>();

        Assert.IsType<NullSnapshotMirror>(mirror);
        Assert.False(mirror.IsConfigured);
        Assert.Empty(await mirror.ListAsync(test.ProjectId));

        // Und der Exportstand entsteht trotzdem — die Spiegelung ist Beiwerk, nie Blocker.
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Schwert";
        await items.SaveItemAsync(context);

        var snapshot = await test.GetService<ExportSnapshotService>()
            .CreateAsync(test.ProjectId, includeAssets: false);

        Assert.True(snapshot.EntryCount > 0);
    }
}
