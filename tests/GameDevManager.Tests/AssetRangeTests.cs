using GameDevManager.Data.Assets;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Grundlage der Range-Anfragen am Asset-Endpunkt: <c>/assets/{id}</c> antwortet mit
/// 206 über <c>Results.Stream(..., enableRangeProcessing: true)</c> — und das trägt nur,
/// wenn der Dateispeicher spulbare Ströme liefert. Genau das steht hier fest: Ein Player,
/// der in einer langen Aufnahme spult, fordert einen Ausschnitt mitten aus der Datei an.
/// </summary>
public class AssetRangeTests
{
    [Fact]
    public async Task Der_Dateispeicher_liefert_spulbare_Stroeme_fuer_Range_Anfragen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gdm-range-{Guid.NewGuid():N}");
        var storage = new FileSystemAssetStorage(new AssetStorageOptions { StoragePath = root, RootPath = root });

        try
        {
            var payload = new byte[1024];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            string storageKey;
            using (var content = new MemoryStream(payload))
            {
                storageKey = await storage.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), ".ogg", content);
            }

            await using var stored = storage.OpenRead(storageKey);

            Assert.NotNull(stored);
            Assert.True(stored.CanSeek);
            Assert.Equal(payload.Length, stored.Length);

            // Der Ausschnitt „bytes=512-515“, wie ihn ein spulender Player anfordert.
            stored.Position = 512;
            var window = new byte[4];
            await stored.ReadExactlyAsync(window);

            Assert.Equal(payload.AsSpan(512, 4).ToArray(), window);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
