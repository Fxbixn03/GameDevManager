using System.Globalization;
using System.Text;

namespace GameDevManager.Domain.Entities;

/// <summary>
/// Die Namensvorlage der Serien-Anlage: „Eisenschwert {n}“ wird zu „Eisenschwert 1“,
/// „Eisenschwert 2“, … Selbst geschrieben statt als Fremdbibliothek — dieselbe Abwägung wie
/// bei <see cref="SimpleMarkdown"/> und dem <c>Csv</c>.
/// <para>
/// Platzhalter, jeweils in geschweiften Klammern:
/// <list type="bullet">
/// <item><c>{n}</c> — die laufende Nummer, bei 1 beginnend.</item>
/// <item><c>{n:001}</c> — die Nummer mit führenden Nullen; die Angabe ist zugleich der
/// Startwert, so wie sie dasteht: <c>{n:010}</c> beginnt bei 010.</item>
/// <item><c>{roemisch}</c> (auch <c>{römisch}</c>) — die Nummer als römische Zahl.</item>
/// <item><c>{liste:Eisen|Stahl|Silber}</c> (auch mit Komma) — die Wörter der Reihe nach;
/// ist die Serie länger als die Liste, beginnt sie von vorn.</item>
/// </list>
/// Ein unbekannter Platzhalter bleibt unverändert stehen — ein Tippfehler darf den Namen
/// nicht verstümmeln, dieselbe Zurückhaltung wie bei den Erwähnungen.
/// </para>
/// </summary>
public static class NameTemplate
{
    /// <summary>Der Name für eine Position der Serie. <paramref name="index"/> beginnt bei 1.</summary>
    public static string Format(string template, int index)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var result = new StringBuilder(template.Length + 8);
        var position = 0;

        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);

            if (open < 0)
            {
                result.Append(template, position, template.Length - position);
                break;
            }

            var close = template.IndexOf('}', open + 1);

            if (close < 0)
            {
                // Eine offene Klammer ohne Gegenstück ist Text, kein Platzhalter.
                result.Append(template, position, template.Length - position);
                break;
            }

            result.Append(template, position, open - position);

            var token = template[(open + 1)..close];
            result.Append(ExpandToken(token, index) ?? $"{{{token}}}");

            position = close + 1;
        }

        return result.ToString();
    }

    /// <summary>Die ganze Serie auf einmal — für die Vorschau und die Anlage.</summary>
    public static List<string> Expand(string template, int count)
    {
        List<string> names = new(Math.Max(count, 0));

        for (var index = 1; index <= count; index++)
        {
            names.Add(Format(template, index));
        }

        return names;
    }

    /// <summary>
    /// Ob die Vorlage überhaupt einen bekannten Platzhalter trägt. Ohne einen ergäbe jede
    /// Position denselben Namen — der Aufrufer hängt dann eine Nummer an.
    /// </summary>
    public static bool HasPlaceholder(string template) =>
        !string.IsNullOrEmpty(template) && Format(template, 1) != Format(template, 2);

    /// <summary>Ein einzelner Platzhalter — <c>null</c>, wenn er unbekannt ist.</summary>
    private static string? ExpandToken(string token, int index)
    {
        if (token.Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        if (token.StartsWith("n:", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = token[2..];

            // Die Angabe ist zugleich der Startwert, so wie sie dasteht: {n:010} beginnt
            // bei 010. Alles außer Ziffern ist kein Zahlenmuster und bleibt stehen.
            if (pattern.Length > 0 && pattern.Length <= 9 && pattern.All(char.IsAsciiDigit))
            {
                var start = int.Parse(pattern, CultureInfo.InvariantCulture);

                return (start + index - 1).ToString(CultureInfo.InvariantCulture)
                    .PadLeft(pattern.Length, '0');
            }

            return null;
        }

        if (token.Equals("roemisch", StringComparison.OrdinalIgnoreCase)
            || token.Equals("römisch", StringComparison.OrdinalIgnoreCase))
        {
            return ToRoman(index);
        }

        if (token.StartsWith("liste:", StringComparison.OrdinalIgnoreCase))
        {
            var words = token[6..]
                .Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return words.Length == 0 ? null : words[(index - 1) % words.Length];
        }

        return null;
    }

    /// <summary>
    /// Die römische Zahl. Jenseits von 3999 gibt es keine übliche Schreibweise — dort bleibt
    /// es bei der arabischen, statt Phantasiezeichen zu erfinden.
    /// </summary>
    public static string ToRoman(int value)
    {
        if (value is < 1 or > 3999)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        (int Value, string Symbol)[] steps =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        ];

        var result = new StringBuilder();

        foreach (var (step, symbol) in steps)
        {
            while (value >= step)
            {
                result.Append(symbol);
                value -= step;
            }
        }

        return result.ToString();
    }
}
