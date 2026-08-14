using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>Stabile Schlüssel der Health Checks — sie beschriften die Zeilen im Zustandsband.</summary>
public static class HealthCheckKeys
{
    public const string DeadItems = "deadItems";
    public const string CraftingCycles = "craftingCycles";
    public const string QuestsWithoutCompletion = "questsWithoutCompletion";
    public const string DialogueDeadEnds = "dialogueDeadEnds";
    public const string OverfullLoot = "overfullLoot";
    public const string OrphanedAssets = "orphanedAssets";
    public const string ImpossibleConditions = "impossibleConditions";
    public const string UnlockCycles = "unlockCycles";
}

/// <summary>
/// Ergebnis einer Prüfung. <paramref name="ModuleKey"/> ist das Modul, in dem sich der Fund
/// beheben lässt — <c>null</c>, wenn er sich über mehrere Module verteilt und nur die
/// Statistik-Seite ihn auflisten kann.
/// </summary>
public sealed record HealthCheckResult(string CheckKey, string? ModuleKey, int Findings);

/// <summary>Alle Prüfungen zusammen, Funde zuerst.</summary>
public sealed record HealthSummary(IReadOnlyList<HealthCheckResult> Checks)
{
    public int TotalFindings => Checks.Sum(check => check.Findings);

    public bool IsClean => TotalFindings == 0;
}

/// <summary>
/// Die Zahlen des Dashboards: zuletzt bearbeitete Entitäten und die Health Checks als
/// Zusammenfassung. Beides führt vorhandene Dienste zusammen und rechnet nichts selbst aus —
/// die Statistik-Seite bleibt die ausführliche Ansicht derselben Prüfungen.
/// </summary>
public class DashboardOverviewService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    StatisticsService statistics,
    CraftingService crafting,
    QuestService quests,
    DialogueService dialogues,
    LootService loot,
    ConditionService conditions,
    TechTreeService techTree)
{
    /// <summary>
    /// Die zuletzt bearbeiteten Entitäten quer durch alle Module, jüngste zuerst.
    /// <para>
    /// Jede Modul-Quelle liefert ihre eigenen <paramref name="count"/> jüngsten Einträge; erst
    /// die Zusammenführung ergibt die tatsächlich jüngsten des Projekts. Leere Module kosten
    /// dabei genau eine Abfrage — in einem jungen Projekt ist das der Normalfall.
    /// </para>
    /// </summary>
    public async Task<List<RecentEntry>> GetRecentlyEditedAsync(
        Guid projectId, int count, CancellationToken ct = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var entries = new List<RecentEntry>();

        foreach (var source in sources)
        {
            entries.AddRange(await source.RecentAsync(db, projectId, count, ct));
        }

        return
        [
            .. entries
                .OrderByDescending(entry => entry.UpdatedAtUtc)
                .ThenBy(entry => entry.Hit.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(count)
        ];
    }

    /// <summary>
    /// Wie viel Inhalt in welchem Bearbeitungsstand steht, über alle Module zusammen.
    /// <para>
    /// Gezählt wird über die <see cref="IModuleEntitySource"/> und nicht über einen
    /// <c>switch</c>: Die Spalte hängt an <see cref="ContentEntity"/> und gilt damit in jedem
    /// Inhaltsmodul — auch in einem künftigen, das dann von selbst mitzählt.
    /// </para>
    /// </summary>
    public async Task<Dictionary<ContentStatus, int>> GetStatusCountsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var counts = new Dictionary<ContentStatus, int>();

        foreach (var source in sources)
        {
            foreach (var entity in await source.LoadAllAsync(db, projectId, ct))
            {
                counts[entity.Status] = counts.GetValueOrDefault(entity.Status) + 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Alle Health Checks des Konzepts als reine Fundzahlen. Funde stehen oben, geprüft-und-sauber
    /// darunter — sonst müsste man die Liste nach dem einen orangen Eintrag absuchen.
    /// <para>
    /// Die Prüfungen laufen nacheinander und teils über den gesamten Bestand eines Moduls
    /// (<see cref="CraftingService.FindCyclesAsync"/> löst den ganzen Rezeptgraphen auf). Das
    /// Dashboard lädt dieses Band deshalb erst nach dem ersten Rendern nach.
    /// </para>
    /// </summary>
    public async Task<HealthSummary> GetHealthAsync(Guid projectId, CancellationToken ct = default)
    {
        List<HealthCheckResult> checks =
        [
            new(HealthCheckKeys.DeadItems, ModuleKeys.Items,
                (await statistics.FindDeadItemsAsync(projectId, ct)).Count),
            new(HealthCheckKeys.CraftingCycles, ModuleKeys.Crafting,
                (await crafting.FindCyclesAsync(projectId, ct)).Count),
            new(HealthCheckKeys.QuestsWithoutCompletion, ModuleKeys.Quests,
                (await quests.FindQuestsWithoutCompletionAsync(projectId, ct)).Count),
            new(HealthCheckKeys.DialogueDeadEnds, ModuleKeys.Dialogs,
                (await dialogues.FindProblemsAsync(projectId, ct)).Count),
            new(HealthCheckKeys.OverfullLoot, ModuleKeys.Loot,
                (await loot.FindOverfullTablesAsync(projectId, ct)).Count),
            new(HealthCheckKeys.OrphanedAssets, ModuleKeys.Assets,
                (await statistics.FindOrphanedAssetsAsync(projectId, ct)).Count),

            // Bedingungen hängen an Entitäten aller Module — ein einzelnes Sprungziel gibt es
            // dafür nicht, die Zeile führt auf die Statistik-Seite.
            new(HealthCheckKeys.ImpossibleConditions, null,
                (await conditions.FindProblemsAsync(projectId, ct)).Count),

            // Ringe im Freischaltungs-Graphen — derselbe Fall wie zyklische Rezepte, nur eine
            // Ebene höher: Alles im Ring wartet auf sich selbst und ist nie erreichbar.
            new(HealthCheckKeys.UnlockCycles, ModuleKeys.TechTree,
                (await techTree.FindCyclesAsync(projectId, ct)).Count)
        ];

        return new HealthSummary(
        [
            .. checks
                .Select((check, position) => (Check: check, Position: position))
                .OrderByDescending(entry => entry.Check.Findings > 0)
                .ThenBy(entry => entry.Position)
                .Select(entry => entry.Check)
        ]);
    }
}
