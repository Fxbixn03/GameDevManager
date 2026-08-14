namespace GameDevManager.Domain.Entities;

/// <summary>
/// Lese- und Schreibregeln für kommagetrennte Stichwortlisten: die Vorlieben und die
/// Persönlichkeit eines <see cref="Npc"/> und jedes Textfeld, das der Nutzer über
/// <see cref="FieldDefinition.IsTagList"/> zur Stichwortliste erklärt hat.
/// <para>
/// Eine eigene Tabelle bekommen sie bewusst nicht: Als kanonische Textspalte gehen sie ohne
/// Zutun durch Export, Import, Duplizieren und — bei Feldwerten — durch die Feldvererbung der
/// Unterarten. Kanonisch heißt getrimmt, ohne Leereinträge und ohne Dubletten, immer mit
/// „, " verbunden; derselbe Stand ergibt so denselben Export.
/// </para>
/// </summary>
public static class KeywordList
{
    /// <summary>Das Trennzeichen — es ist deshalb nie Teil eines Stichworts.</summary>
    public const char Separator = ',';

    /// <summary>Die einzelnen Stichwörter einer Textspalte, in ihrer Reihenfolge.</summary>
    public static List<string> Parse(string? value) =>
    [
        .. (value ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    /// <summary>Schreibt Stichwörter kanonisch; keine ergeben <c>null</c> statt einer leeren Zeile.</summary>
    public static string? Format(IEnumerable<string> entries)
    {
        var parts = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parts.Count == 0 ? null : string.Join($"{Separator} ", parts);
    }

    /// <summary>Bringt eine bestehende Textspalte auf dieselbe kanonische Form.</summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Format(Parse(value));
}
