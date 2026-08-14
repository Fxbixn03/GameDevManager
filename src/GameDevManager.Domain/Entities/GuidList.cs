namespace GameDevManager.Domain.Entities;

/// <summary>
/// Lese- und Schreibregeln für Referenzlisten: mehrere Entitäten in <b>einem</b> Feld — die drei
/// Effekte eines Schwerts, die vier erlaubten Klassen einer Rüstung.
/// <para>
/// Gespeichert wird kanonisch semikolongetrennt in <see cref="FieldValue.TextValue"/>, genau
/// wie <see cref="KeywordList"/> es für Stichwörter tut, und aus demselben Grund: Als Textspalte
/// geht die Liste ohne Zutun durch Export, Import, Duplizieren und die Feldvererbung der
/// Unterarten. Das <b>Duplizieren</b> trifft sie sogar von selbst — <c>GuidRemap</c> tauscht über
/// den gesamten JSON-Text und erkennt jede GUID daran, wie sie aussieht, nicht daran, in welcher
/// Spalte sie steht.
/// </para>
/// <para>
/// Semikolon und nicht Komma: Das Komma trennt Stichwörter, und die beiden Listen stehen in
/// derselben Spalte — ein gemeinsames Trennzeichen machte aus einem umgestellten Feld stillen
/// Unsinn statt eines leeren Wertes.
/// </para>
/// </summary>
public static class GuidList
{
    /// <summary>Das Trennzeichen. In einer GUID kommt es nicht vor.</summary>
    public const char Separator = ';';

    /// <summary>
    /// Die einzelnen GUIDs einer Textspalte, in ihrer Reihenfolge. Was keine GUID ist, fällt
    /// heraus: Ein Feld, das erst später zur Referenzliste wurde, trägt noch seinen alten Text
    /// und darf davon nicht umfallen — dieselbe Zurückhaltung wie beim Kurven-JSON.
    /// </summary>
    public static List<Guid> Parse(string? value)
    {
        var result = new List<Guid>();

        foreach (var part in (value ?? string.Empty)
                     .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id) && !result.Contains(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Schreibt GUIDs kanonisch — kleingeschrieben mit Bindestrichen, ohne Dubletten, mit
    /// „; “ verbunden. Keine ergeben <c>null</c> statt einer leeren Zeile.
    /// </summary>
    public static string? Format(IEnumerable<Guid> entries)
    {
        var parts = entries.Where(id => id != Guid.Empty).Distinct().ToList();

        return parts.Count == 0 ? null : string.Join($"{Separator} ", parts);
    }

    /// <summary>Bringt eine bestehende Textspalte auf dieselbe kanonische Form.</summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Format(Parse(value));
}
