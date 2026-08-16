namespace GameDevManager.Data.Services;

/// <summary>
/// Der unscharfe Namensvergleich der Kommandopalette: Die eingetippten Zeichen müssen der
/// Reihe nach im Ziel vorkommen, nicht zusammenhängend — „ei schwert“ findet damit das
/// Eisenschwert, und „nmi“ das „Neu: Items“.
/// <para>
/// Bewusst hier und nicht in der Komponente: Eine Regel, nach der die halbe Bedienung sucht,
/// gehört an eine Stelle, an der sie sich prüfen lässt.
/// </para>
/// <para>
/// Die globale Suche bleibt beim gewöhnlichen <c>Contains</c>. Sie durchsucht den <b>Inhalt</b>
/// — Dialogzeilen, Feldwerte —, und dort wäre ein unscharfer Treffer eher Rauschen als Fund.
/// </para>
/// </summary>
public static class FuzzyMatch
{
    /// <summary>
    /// Ob <paramref name="candidate"/> zur Eingabe passt. Leerzeichen der Eingabe zählen nicht
    /// mit: Sie trennen nur im Kopf des Tippenden. Groß-/Kleinschreibung ist egal.
    /// </summary>
    public static bool Matches(string? query, string? candidate)
    {
        var needle = (query ?? string.Empty).Replace(" ", string.Empty);

        if (needle.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var position = 0;

        foreach (var character in candidate)
        {
            if (position < needle.Length
                && char.ToLowerInvariant(character) == char.ToLowerInvariant(needle[position]))
            {
                position++;
            }
        }

        return position == needle.Length;
    }
}
