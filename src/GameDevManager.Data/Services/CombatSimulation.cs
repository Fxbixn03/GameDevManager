namespace GameDevManager.Data.Services;

/// <summary>Ein Kämpfer, auf die vier Rollen der Zuordnung heruntergerechnet.</summary>
public sealed record CombatantStats(string Name, double Health, double Damage, double Defense, double Speed);

/// <summary>
/// Das Ergebnis eines Laufs — als Verteilung, nicht nur als Mittelwert: Bei „Runden bis
/// K.O.“ sagt der Median mehr, und die Ausreißer sind genau das, was ein Designer sucht.
/// </summary>
public sealed record CombatResult(
    int Fights,
    int WinsA,
    int WinsB,
    int Draws,
    double MedianRounds,
    int MinRounds,
    int MaxRounds,
    double AverageDamagePerRoundA,
    double AverageDamagePerRoundB,
    double HitChanceA,
    double HitChanceB);

/// <summary>
/// Der Kampf-Simulator: zwei Wertesätze gegeneinander, zehntausendmal — nach dem Muster des
/// Loot-Simulators eine reine Rechenklasse ohne eigenen Datenbestand.
/// <para>
/// Das Kampfmodell ist bewusst einfach und steht vollständig hier: <b>Trefferchance</b>
/// 75 % plus 5 Prozentpunkte je Tempo-Vorsprung (begrenzt auf 5–95 %), <b>Schaden je
/// Treffer</b> ist Schaden minus Verteidigung, mindestens 1, gestreut um ±15 %. Je Runde
/// schlagen beide, der Schnellere zuerst; wer fällt, schlägt nicht mehr zurück. Nach
/// <see cref="MaxRounds"/> Runden gilt der Kampf als unentschieden — zwei Schildkröten
/// sollen den Lauf nicht aufhängen.
/// </para>
/// <para>
/// <b>Der Startwert kommt vom Aufrufer</b>, nicht aus <c>Random.Shared</c>: Derselbe
/// Startwert muss denselben Lauf ergeben, sonst ließe sich eine Wertänderung nicht gegen
/// den Lauf von vorhin halten — dieselbe Regel wie beim Loot-Simulator.
/// </para>
/// </summary>
public static class CombatSimulation
{
    public const int DefaultFights = 1000;

    /// <summary>Notbremse je Kampf: Danach ist es ein Patt, kein Endloslauf.</summary>
    public const int MaxRounds = 500;

    public static CombatResult Run(
        CombatantStats a, CombatantStats b, int seed, int fights = DefaultFights)
    {
        var random = new Random(seed);

        var winsA = 0;
        var winsB = 0;
        var draws = 0;
        var rounds = new List<int>(fights);
        var damageA = 0d;
        var damageB = 0d;
        var totalRounds = 0L;

        for (var fight = 0; fight < fights; fight++)
        {
            var healthA = a.Health;
            var healthB = b.Health;
            var round = 0;

            while (round < MaxRounds && healthA > 0 && healthB > 0)
            {
                round++;

                // Der Schnellere zuerst; bei Gleichstand entscheidet je Kampf der Würfel —
                // ein fester Vorteil für Seite A wäre eine stille Hausregel.
                var aFirst = a.Speed > b.Speed || (a.Speed == b.Speed && random.Next(2) == 0);

                if (aFirst)
                {
                    healthB -= Strike(random, a, b, ref damageA);
                    if (healthB > 0)
                    {
                        healthA -= Strike(random, b, a, ref damageB);
                    }
                }
                else
                {
                    healthA -= Strike(random, b, a, ref damageB);
                    if (healthA > 0)
                    {
                        healthB -= Strike(random, a, b, ref damageA);
                    }
                }
            }

            totalRounds += round;

            if (healthA <= 0 && healthB <= 0)
            {
                draws++;
            }
            else if (healthB <= 0)
            {
                winsA++;
                rounds.Add(round);
            }
            else if (healthA <= 0)
            {
                winsB++;
                rounds.Add(round);
            }
            else
            {
                draws++;
            }
        }

        rounds.Sort();

        return new CombatResult(
            fights,
            winsA,
            winsB,
            draws,
            Median(rounds),
            rounds.Count > 0 ? rounds[0] : 0,
            rounds.Count > 0 ? rounds[^1] : 0,
            totalRounds > 0 ? damageA / totalRounds : 0,
            totalRounds > 0 ? damageB / totalRounds : 0,
            HitChance(a, b),
            HitChance(b, a));
    }

    /// <summary>75 % plus 5 Prozentpunkte je Tempo-Vorsprung, begrenzt auf 5–95 %.</summary>
    public static double HitChance(CombatantStats attacker, CombatantStats defender) =>
        Math.Clamp(0.75 + (attacker.Speed - defender.Speed) * 0.05, 0.05, 0.95);

    /// <summary>Ein Schlag: erst der Treffer-Wurf, dann Schaden minus Verteidigung, mindestens 1, ±15 %.</summary>
    private static double Strike(
        Random random, CombatantStats attacker, CombatantStats defender, ref double dealtTotal)
    {
        if (random.NextDouble() >= HitChance(attacker, defender))
        {
            return 0;
        }

        var dealt = Math.Max(1, attacker.Damage - defender.Defense) * (0.85 + random.NextDouble() * 0.3);
        dealtTotal += dealt;

        return dealt;
    }

    private static double Median(List<int> sorted) => sorted.Count switch
    {
        0 => 0,
        var count when count % 2 == 1 => sorted[count / 2],
        var count => (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0
    };
}
