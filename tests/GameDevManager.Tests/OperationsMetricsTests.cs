using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Betriebs-Kennzahlen: was eine Überwachung von außen sehen darf. Reine Auswertung —
/// geprüft wird, dass gezählt wird, was da ist, und dass „nie gesichert“ nicht als „gerade
/// eben gesichert“ herauskommt.
/// </summary>
public class OperationsMetricsTests
{
    private static async Task SeedItemAsync(TestDatabase test, string name)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);
    }

    [Fact]
    public async Task Die_Datenbank_antwortet()
    {
        using var test = new TestDatabase();

        var (reachable, error) = await test.GetService<OperationsMetricsService>().CheckDatabaseAsync();

        Assert.True(reachable);
        Assert.Null(error);
    }

    [Fact]
    public async Task Gezaehlt_wird_ueber_alle_Module()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Schwert");
        await SeedItemAsync(test, "Axt");

        var metrics = await test.GetService<OperationsMetricsService>().CollectAsync();

        Assert.True(metrics.DatabaseReachable);
        Assert.Equal(1, metrics.ProjectCount);
        Assert.Equal(2, metrics.ContentCount);
    }

    [Fact]
    public async Task Ohne_Stand_fehlt_die_Alterszahl()
    {
        using var test = new TestDatabase();

        var metrics = await test.GetService<OperationsMetricsService>().CollectAsync();

        Assert.Null(metrics.NewestSnapshotAgeHours);

        // Die Reihe fehlt dann auch im Prometheus-Text — sonst läse eine Überwachung
        // „nie gesichert“ als „gerade eben gesichert“.
        var text = OperationsMetricsService.ToPrometheus(metrics);

        Assert.DoesNotContain("gdm_newest_snapshot_age_hours", text, StringComparison.Ordinal);
        Assert.Contains("gdm_database_reachable 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Exportstand_taucht_in_den_Zahlen_auf()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Schwert");

        await test.GetService<ExportSnapshotService>().CreateAsync(test.ProjectId, includeAssets: false);

        var metrics = await test.GetService<OperationsMetricsService>().CollectAsync();

        Assert.Equal(1, metrics.SnapshotCount);
        Assert.NotNull(metrics.NewestSnapshotAgeHours);
        Assert.InRange(metrics.NewestSnapshotAgeHours!.Value, 0, 1);

        Assert.Contains(
            "gdm_newest_snapshot_age_hours",
            OperationsMetricsService.ToPrometheus(metrics),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Zahlen_stehen_im_Prometheus_Text_in_fester_Kultur()
    {
        var metrics = new OperationsMetrics(true, null, 1, 2, 3, 4, 5678, 1, 1.5);

        // Ein Komma wäre dort kein Dezimaltrenner — dieselbe Regel wie bei den Kurvenausdrücken.
        Assert.Contains("gdm_newest_snapshot_age_hours 1.5", OperationsMetricsService.ToPrometheus(metrics),
            StringComparison.Ordinal);
    }
}
