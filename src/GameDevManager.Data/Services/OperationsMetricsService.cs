using GameDevManager.Data.Assets;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Kennzahlen für die Überwachung eines laufenden Betriebs — nicht für die Oberfläche.
/// </summary>
/// <param name="DatabaseReachable">Antwortet die Datenbank? Die einzige Prüfung, die wirklich hart ist.</param>
/// <param name="AssetBytes">Belegter Platz im Asset-Verzeichnis.</param>
/// <param name="NewestSnapshotAgeHours">
/// Alter des jüngsten Exportstands über alle Projekte, <c>null</c> wenn es keinen gibt — die
/// Zahl, an der eine Überwachung merkt, dass die Sicherung ausgefallen ist.
/// </param>
public sealed record OperationsMetrics(
    bool DatabaseReachable,
    string? DatabaseError,
    int ProjectCount,
    int ContentCount,
    int UserCount,
    int AssetCount,
    long AssetBytes,
    int SnapshotCount,
    double? NewestSnapshotAgeHours,
    IReadOnlyList<BackgroundRunInfo> BackgroundRuns);

/// <summary>
/// Sammelt die Betriebs-Kennzahlen. Reine Auswertung ohne eigenen Datenbestand — dasselbe
/// Muster wie der Freischaltungs-Graph und der Loot-Simulator.
/// <para>
/// Die Zahlen verraten die Größe des Bestands; ausgeliefert werden sie deshalb nur hinter
/// einem API-Schlüssel, nicht offen. Ausgenommen ist die reine Verfügbarkeitsprüfung — dass
/// die Anwendung läuft, sieht ohnehin jeder, der sie erreicht.
/// </para>
/// </summary>
public class OperationsMetricsService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    IAssetStorage assetStorage,
    AssetStorageOptions assetOptions,
    ExportStorageOptions exportOptions,
    BackgroundRunTracker backgroundRuns)
{
    /// <summary>
    /// Ob die Datenbank antwortet. Die Frage, die eine Überwachung zuerst stellt — und die
    /// einzige, die ohne Schlüssel beantwortet wird.
    /// </summary>
    public async Task<(bool Reachable, string? Error)> CheckDatabaseAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            return (await db.Database.CanConnectAsync(ct), null);
        }
        catch (Exception ex)
        {
            // Eine nicht erreichbare Datenbank ist genau der Fall, den die Überwachung sucht —
            // sie darf den Endpunkt nicht mit einem Stapelabzug beantworten.
            return (false, ex.Message);
        }
    }

    public async Task<OperationsMetrics> CollectAsync(CancellationToken ct = default)
    {
        var (reachable, error) = await CheckDatabaseAsync(ct);

        if (!reachable)
        {
            // Die Hintergrundläufe stehen trotzdem da — gerade wenn die Datenbank klemmt,
            // ist ihre Fehlerzahl die interessante Auskunft.
            return new OperationsMetrics(false, error, 0, 0, 0, 0, 0, 0, null, backgroundRuns.GetAll());
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var projects = await db.GameProjects.AsNoTracking().Select(project => project.Id).ToListAsync(ct);

        // Gezählt wird über die Modul-Quellen, damit ein neues Modul von selbst mitzählt —
        // dieselbe Überlegung wie beim Bearbeitungsstand des Dashboards.
        var content = 0;
        foreach (var projectId in projects)
        {
            foreach (var source in sources)
            {
                content += (await source.LoadAllAsync(db, projectId, ct)).Count;
            }
        }

        var assetBytes = DirectorySize(assetOptions.RootPath);
        var snapshots = SnapshotFiles();

        return new OperationsMetrics(
            true,
            null,
            projects.Count,
            content,
            await db.AppUsers.CountAsync(ct),
            await db.Assets.CountAsync(ct),
            assetBytes,
            snapshots.Count,
            snapshots.Count == 0
                ? null
                : (DateTime.UtcNow - snapshots.Max(File.GetLastWriteTimeUtc)).TotalHours,
            backgroundRuns.GetAll());
    }

    /// <summary>
    /// Der belegte Platz eines Verzeichnisses. Gelaufen wird über die Dateien statt über die
    /// Datenbank: Gefragt ist, was auf der Platte liegt — verwaiste Dateien eingeschlossen.
    /// </summary>
    private static long DirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        // Unlesbare Unterverzeichnisse und Dateien, die zwischen Auflisten und Nachsehen
        // verschwinden, überspringt der Lauf: Eine Kennzahl darf nicht daran scheitern, dass
        // im Verzeichnis etwas liegt, das uns nicht gehört.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        long total = 0;

        foreach (var file in Directory.EnumerateFiles(path, "*", options))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Weiterzählen — eine Datei weniger ist besser als keine Zahl.
            }
        }

        return total;
    }

    private List<string> SnapshotFiles() =>
        Directory.Exists(exportOptions.RootPath)
            ? [.. Directory.EnumerateFiles(exportOptions.RootPath, "*.zip")]
            : [];

    /// <summary>
    /// Dieselben Zahlen im Prometheus-Textformat. Bewusst von Hand geschrieben statt über eine
    /// Fremdbibliothek — es sind acht Zeilen, und das Format besteht aus Name, Wert und einer
    /// Zeile Kommentar davor.
    /// </summary>
    public static string ToPrometheus(OperationsMetrics metrics)
    {
        var builder = new System.Text.StringBuilder();

        void Write(string name, string help, double value)
        {
            builder.AppendLine($"# HELP {name} {help}");
            builder.AppendLine($"# TYPE {name} gauge");
            builder.AppendLine(
                $"{name} {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        Write("gdm_database_reachable", "1 if the database answers, 0 otherwise.",
            metrics.DatabaseReachable ? 1 : 0);
        Write("gdm_projects", "Number of game projects.", metrics.ProjectCount);
        Write("gdm_content_entities", "Number of content entities across all modules.", metrics.ContentCount);
        Write("gdm_users", "Number of accounts.", metrics.UserCount);
        Write("gdm_assets", "Number of asset rows.", metrics.AssetCount);
        Write("gdm_asset_bytes", "Bytes occupied by the asset directory.", metrics.AssetBytes);
        Write("gdm_export_snapshots", "Number of retained export snapshots.", metrics.SnapshotCount);

        // Ein fehlender Stand ist keine Null Stunden — die Reihe bleibt in diesem Fall weg,
        // damit eine Überwachung „nie gesichert“ nicht als „gerade eben gesichert“ liest.
        if (metrics.NewestSnapshotAgeHours is { } age)
        {
            Write("gdm_newest_snapshot_age_hours", "Age of the newest export snapshot in hours.", age);
        }

        // Die Hintergrundläufe als Reihen mit Label — ein Dienst, der noch nie lief, hat
        // keine Reihe: „nie gelaufen“ darf nicht wie „gerade gelaufen“ aussehen, dieselbe
        // Überlegung wie beim Alter des Exportstands.
        if (metrics.BackgroundRuns.Count > 0)
        {
            void WriteLabeled(string name, string help, Func<BackgroundRunInfo, double> value)
            {
                builder.AppendLine($"# HELP {name} {help}");
                builder.AppendLine($"# TYPE {name} gauge");

                foreach (var run in metrics.BackgroundRuns)
                {
                    builder.AppendLine(string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{name}{{service=\"{run.Service}\"}} {value(run)}"));
                }
            }

            WriteLabeled("gdm_background_last_run_age_seconds",
                "Seconds since the service last finished a run.",
                run => (DateTime.UtcNow - run.LastRunUtc).TotalSeconds);
            WriteLabeled("gdm_background_last_run_seconds",
                "Duration of the last run in seconds.",
                run => run.LastDurationSeconds);
            WriteLabeled("gdm_background_last_run_failed",
                "1 if the last run ended with an error, 0 otherwise.",
                run => run.LastRunFailed ? 1 : 0);
            WriteLabeled("gdm_background_errors_total",
                "Failed runs since process start.",
                run => run.ErrorCount);
        }

        return builder.ToString();
    }
}
