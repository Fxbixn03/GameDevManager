using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Das Ergebnis eines Eintrags über den ganzen Lauf. Gezählt wird je <b>Eintrag</b> und nicht
/// je Item: Dasselbe Item darf mehrfach in einer Tabelle stehen („zu 50 % eine Münze, zu 5 %
/// gleich zwanzig“), und genau diese zwei Zeilen will der Designer nebeneinander sehen.
/// </summary>
/// <param name="Drops">Wie oft der Eintrag gefallen ist.</param>
/// <param name="DropRate">Anteil der Würfe mit diesem Eintrag, in Prozent.</param>
/// <param name="ExpectedPerRoll">Erwartete Stückzahl je Wurf — die Zahl fürs Balancing.</param>
/// <param name="MedianWait">
/// Median der Würfe bis zum nächsten Treffer — „wie lange wartet ein Spieler auf das seltene
/// Schwert“. <c>null</c>, wenn der Eintrag im ganzen Lauf nie gefallen ist.
/// </param>
public sealed record LootSimulationRow(
    Guid EntryId,
    Guid ItemId,
    string ItemName,
    double Chance,
    int Drops,
    double DropRate,
    long TotalQuantity,
    double AverageQuantity,
    double ExpectedPerRoll,
    int? MedianWait);

/// <summary>Was ein Simulationslauf über eine Loot-Table sagt.</summary>
public sealed record LootSimulationResult(
    int Rolls,
    int Seed,
    LootRollMode RollMode,
    int EmptyRolls,
    double EmptyRate,
    double AverageItemsPerRoll,
    List<LootSimulationRow> Rows);

/// <summary>
/// Würfelt eine Loot-Table viele tausend Mal aus und fasst zusammen, was dabei herauskommt.
/// <para>
/// Reine Auswertung ohne eigenen Datenbestand — dasselbe Muster wie der Freischaltungs-Graph.
/// Der Zufallsgenerator bekommt seinen Startwert von außen: Derselbe Startwert muss dasselbe
/// Ergebnis liefern, sonst ließe sich eine Änderung an der Tabelle nicht gegen den Lauf von
/// vorhin halten.
/// </para>
/// </summary>
public static class LootSimulation
{
    /// <summary>So oft wird gewürfelt, wenn die Oberfläche nichts anderes sagt.</summary>
    public const int DefaultRolls = 10_000;

    /// <summary>
    /// Obergrenze für einen Lauf. Bei einer Million Würfen über eine große Tabelle geht es um
    /// Sekunden — darüber wartet niemand mehr auf eine Zahl, die sich kaum noch ändert.
    /// </summary>
    public const int MaxRolls = 1_000_000;

    public static LootSimulationResult Run(
        LootTable table, IReadOnlyDictionary<Guid, string> itemNames, int rolls, int seed)
    {
        rolls = Math.Clamp(rolls, 1, MaxRolls);

        var entries = table.Entries.OrderBy(entry => entry.SortOrder).ThenBy(entry => entry.Id).ToList();
        var random = new Random(seed);

        var drops = new int[entries.Count];
        var quantities = new long[entries.Count];

        // Für den Median der Wartezeit: der Wurf, in dem der Eintrag zuletzt fiel, und die
        // Abstände dazwischen. Gesammelt statt gemittelt — der Mittelwert einer Wartezeit sagt
        // bei seltenen Dingen weniger als die Mitte der Verteilung.
        var lastDrop = new int[entries.Count];
        var waits = new List<int>[entries.Count];

        for (var index = 0; index < entries.Count; index++)
        {
            lastDrop[index] = -1;
            waits[index] = [];
        }

        var emptyRolls = 0;
        long totalItems = 0;

        for (var roll = 0; roll < rolls; roll++)
        {
            var anything = false;

            if (table.RollMode == LootRollMode.SinglePick)
            {
                // Ein Wurf für alle: Die Wahrscheinlichkeiten liegen hintereinander auf der
                // Skala von 0 bis 100. Was über 100 hinausragt, ist unerreichbar — genau das
                // meldet der Health Check.
                var pick = random.NextDouble() * 100;
                var cursor = 0d;

                for (var index = 0; index < entries.Count; index++)
                {
                    cursor += entries[index].Chance;

                    if (pick < cursor)
                    {
                        Record(index, roll);
                        anything = true;
                        break;
                    }
                }
            }
            else
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    if (random.NextDouble() * 100 < entries[index].Chance)
                    {
                        Record(index, roll);
                        anything = true;
                    }
                }
            }

            if (!anything)
            {
                emptyRolls++;
            }

            void Record(int index, int currentRoll)
            {
                var entry = entries[index];
                var low = Math.Min(entry.MinQuantity, entry.MaxQuantity);
                var high = Math.Max(entry.MinQuantity, entry.MaxQuantity);
                var quantity = random.Next(low, high + 1);

                drops[index]++;
                quantities[index] += quantity;
                totalItems += quantity;

                waits[index].Add(currentRoll - lastDrop[index]);
                lastDrop[index] = currentRoll;
            }
        }

        var rows = new List<LootSimulationRow>(entries.Count);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];

            rows.Add(new LootSimulationRow(
                entry.Id,
                entry.ItemId,
                itemNames.GetValueOrDefault(entry.ItemId, string.Empty),
                entry.Chance,
                drops[index],
                100d * drops[index] / rolls,
                quantities[index],
                drops[index] == 0 ? 0 : (double)quantities[index] / drops[index],
                (double)quantities[index] / rolls,
                Median(waits[index])));
        }

        return new LootSimulationResult(
            rolls,
            seed,
            table.RollMode,
            emptyRolls,
            100d * emptyRolls / rolls,
            (double)totalItems / rolls,
            rows);
    }

    private static int? Median(List<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();

        // Bei gerader Anzahl der untere der beiden mittleren Werte: Eine Wartezeit ist eine
        // Anzahl Würfe, und „3,5 Versuche“ wäre keine Auskunft, sondern eine Rechnung.
        return values[(values.Count - 1) / 2];
    }
}
