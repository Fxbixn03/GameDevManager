using GameDevManager.Domain.Curves;

namespace GameDevManager.Data.Services;

/// <summary>Eine Stufe der Zeitachse: was sie kostet, was zu grinden bleibt, wie lange das dauert.</summary>
/// <param name="StepXp">XP für den Schritt von dieser Stufe zur nächsten — der Wert der Kurve an x.</param>
/// <param name="GrindXp">Was nach Abzug des Quest-Topfs für diesen Schritt übrig bleibt.</param>
/// <param name="Hours">Erwartete Spielzeit für den Schritt — <c>null</c>, wenn keine Rate bekannt ist.</param>
/// <param name="IsBottleneck">Der Schritt kostet mehr als das Doppelte des Medians — der Engpass.</param>
public sealed record ProgressionStep(
    int Level,
    double StepXp,
    double CumulativeXp,
    double GrindXp,
    double? Hours,
    bool IsBottleneck);

/// <summary>
/// Die Progressions-Simulation: Wie lange braucht ein Spieler bis Stufe X? Reine Rechnung
/// nach dem Muster von <see cref="CombatSimulation"/> — kein eigener Datenbestand, alles
/// kommt aus dem, was schon da ist: die <b>Stufen-Kurve</b> (ein Kurvenfeld — ihr Wert an
/// <c>x</c> ist das XP für den Schritt von Stufe <c>x</c> auf <c>x+1</c>), der
/// <b>Quest-Topf</b> (die Summe eines XP-Felds über alle Quests, einmaliges XP, das die
/// ersten Stufen trägt) und die <b>Rate</b> (XP je Stunde aus wiederholbaren Quellen —
/// Mob-XP mal Kills je Stunde).
/// <para>
/// <b>Engpässe</b> sind Schritte, die mehr als das Doppelte des Medians kosten: Genau die
/// Stellen, an denen sich Spieler festbeißen, und genau die, die ein Designer sucht.
/// Stellen, an denen die Kurve nichts hergibt (Formel-Loch), fallen aus der Achse heraus,
/// statt sie zu verwerfen — dieselbe Zurückhaltung wie bei der Wertetabelle der Kurven.
/// </para>
/// </summary>
public static class ProgressionSimulation
{
    public static List<ProgressionStep> Run(CurveDefinition curve, double questXp, double xpPerHour)
    {
        var steps = new List<ProgressionStep>();
        var stepXp = new List<(int Level, double Xp)>();

        var from = (int)Math.Ceiling(curve.From);
        var to = (int)Math.Floor(curve.To);

        for (var level = from; level <= to; level++)
        {
            if (curve.ValueAt(level) is { } xp && xp > 0)
            {
                stepXp.Add((level, xp));
            }
        }

        if (stepXp.Count == 0)
        {
            return steps;
        }

        // Der Engpass misst sich am Median, nicht am Mittelwert — eine steile Endkurve
        // verschöbe sonst die Grenze, und die frühen Engpässe blieben unsichtbar.
        List<double> sorted = [.. stepXp.Select(step => step.Xp).OrderBy(xp => xp)];
        var median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

        var cumulative = 0d;
        var remainingQuestXp = Math.Max(0, questXp);

        foreach (var (level, xp) in stepXp)
        {
            cumulative += xp;

            // Der Quest-Topf trägt die ersten Stufen: einmaliges XP, das der Reihe nach
            // aufgebraucht wird — was danach fehlt, ist Grind.
            var fromQuests = Math.Min(remainingQuestXp, xp);
            remainingQuestXp -= fromQuests;
            var grind = xp - fromQuests;

            steps.Add(new ProgressionStep(
                level,
                xp,
                cumulative,
                grind,
                xpPerHour > 0 ? grind / xpPerHour : null,
                xp > median * 2));
        }

        return steps;
    }
}
