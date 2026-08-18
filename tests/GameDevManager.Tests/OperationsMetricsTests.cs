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
        var metrics = new OperationsMetrics(true, null, 1, 2, 3, 4, 5678, 1, 1.5, [], []);

        // Ein Komma wäre dort kein Dezimaltrenner — dieselbe Regel wie bei den Kurvenausdrücken.
        Assert.Contains("gdm_newest_snapshot_age_hours 1.5", OperationsMetricsService.ToPrometheus(metrics),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- Je Projekt

    [Fact]
    public async Task Kennzahlen_je_Projekt_zaehlen_Inhalte_und_Staende_mit_der_GUID_als_Label()
    {
        using var test = new TestDatabase();
        await SeedItemAsync(test, "Schwert");
        await test.GetService<ExportSnapshotService>().CreateAsync(test.ProjectId, includeAssets: false);

        var metrics = await test.GetService<OperationsMetricsService>().CollectAsync();

        var project = Assert.Single(metrics.Projects);
        Assert.Equal(test.ProjectId, project.ProjectId);
        Assert.Equal(1, project.ContentCount);
        Assert.Equal(1, project.SnapshotCount);
        Assert.NotNull(project.NewestSnapshotAgeHours);

        // Im Prometheus-Text steht die GUID als Label — und kein Name.
        var text = OperationsMetricsService.ToPrometheus(metrics);
        Assert.Contains(
            $"gdm_project_content_entities{{project=\"{test.ProjectId:D}\"}} 1",
            text, StringComparison.Ordinal);
        Assert.DoesNotContain("Schwert", text, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------- Hintergrundläufe

    [Fact]
    public async Task Hintergrundlaeufe_stehen_in_beiden_Formaten()
    {
        using var test = new TestDatabase();

        var tracker = test.GetService<BackgroundRunTracker>();
        tracker.Record(BackgroundRunTracker.ChangeLogMaintenance, TimeSpan.FromSeconds(1.5), success: true);
        tracker.Record(BackgroundRunTracker.WebhookDispatcher, TimeSpan.FromSeconds(0.2), success: false);

        var metrics = await test.GetService<OperationsMetricsService>().CollectAsync();

        Assert.Equal(2, metrics.BackgroundRuns.Count);

        var maintenance = metrics.BackgroundRuns.Single(
            run => run.Service == BackgroundRunTracker.ChangeLogMaintenance);
        Assert.False(maintenance.LastRunFailed);
        Assert.Equal(0, maintenance.ErrorCount);
        Assert.Equal(1.5, maintenance.LastDurationSeconds);

        var dispatcher = metrics.BackgroundRuns.Single(
            run => run.Service == BackgroundRunTracker.WebhookDispatcher);
        Assert.True(dispatcher.LastRunFailed);
        Assert.Equal(1, dispatcher.ErrorCount);

        var text = OperationsMetricsService.ToPrometheus(metrics);

        // In fester Kultur, mit dem Dienst als Label.
        Assert.Contains(
            "gdm_background_last_run_seconds{service=\"changelog_maintenance\"} 1.5",
            text, StringComparison.Ordinal);
        Assert.Contains(
            "gdm_background_errors_total{service=\"webhook_dispatcher\"} 1",
            text, StringComparison.Ordinal);
        Assert.Contains(
            "gdm_background_last_run_failed{service=\"webhook_dispatcher\"} 1",
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_nie_gelaufener_Dienst_hat_keine_Reihe()
    {
        var metrics = new OperationsMetrics(true, null, 1, 0, 1, 0, 0, 0, null, [], []);

        Assert.DoesNotContain(
            "gdm_background_", OperationsMetricsService.ToPrometheus(metrics), StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_erfolgreicher_Lauf_setzt_die_Fehlerzahl_nicht_zurueck()
    {
        var tracker = new BackgroundRunTracker();

        tracker.Record("test", TimeSpan.FromSeconds(1), success: false);
        tracker.Record("test", TimeSpan.FromSeconds(1), success: true);

        var run = Assert.Single(tracker.GetAll());
        Assert.False(run.LastRunFailed);
        Assert.Equal(1, run.ErrorCount);
    }
}
