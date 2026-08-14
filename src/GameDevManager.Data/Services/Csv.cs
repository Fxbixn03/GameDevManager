using System.Text;

namespace GameDevManager.Data.Services;

/// <summary>
/// Lesen und Schreiben von CSV nach RFC 4180 — ohne Fremdbibliothek, dieselbe Abwägung wie
/// beim <c>ImageDimensionReader</c> und beim Ausdrucksrechner der Kurven: Ein Format aus drei
/// Regeln (Trennzeichen, Anführungszeichen, verdoppeltes Anführungszeichen) ist billiger
/// selbst geschrieben als samt Abhängigkeit gepflegt.
/// </summary>
public static class Csv
{
    /// <summary>
    /// Das Trennzeichen, das beim Schreiben benutzt wird. Semikolon und nicht Komma, weil das
    /// Balancing in Tabellenprogrammen mit deutscher Ländereinstellung landet — dort ist das
    /// Komma das Dezimaltrennzeichen und ein komma-getrenntes CSV eine einzige Spalte.
    /// Gelesen wird trotzdem beides, siehe <see cref="DetectSeparator"/>.
    /// </summary>
    public const char Separator = ';';

    /// <summary>
    /// Rät das Trennzeichen aus der Kopfzeile: Wer sein CSV woanders erzeugt hat, soll es
    /// nicht erst umbauen müssen. Gezählt wird außerhalb von Anführungszeichen, sonst
    /// entschiede ein Semikolon in einem Beschreibungstext mit.
    /// </summary>
    public static char DetectSeparator(string content)
    {
        var line = FirstLine(content);

        return CountOutsideQuotes(line, ',') > CountOutsideQuotes(line, Separator) ? ',' : Separator;
    }

    /// <summary>Schreibt eine Zeile. <c>null</c> wird zur leeren Zelle.</summary>
    public static string FormatRow(IEnumerable<string?> cells, char separator = Separator) =>
        string.Join(separator, cells.Select(cell => Quote(cell, separator)));

    /// <summary>
    /// Zerlegt einen ganzen CSV-Text in Zeilen aus Zellen. Zeilenumbrüche innerhalb von
    /// Anführungszeichen gehören zur Zelle — mehrzeilige Beschreibungen gehen sonst verloren.
    /// </summary>
    public static List<List<string>> Parse(string content, char separator)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();

        var quoted = false;
        var index = 0;

        // Ein BOM am Anfang gehört nicht zur ersten Spaltenüberschrift — sonst hieße die
        // Spalte „﻿id“ und würde nie gefunden.
        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            index = 1;
        }

        void EndCell()
        {
            row.Add(cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();

            // Eine Zeile aus einer einzigen leeren Zelle ist eine Leerzeile, kein Datensatz.
            if (row.Count > 1 || !string.IsNullOrEmpty(row[0]))
            {
                rows.Add([.. row]);
            }

            row.Clear();
        }

        while (index < content.Length)
        {
            var current = content[index];

            if (quoted)
            {
                if (current == '"')
                {
                    // Zwei Anführungszeichen hintereinander sind ein echtes Anführungszeichen.
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        cell.Append('"');
                        index += 2;
                        continue;
                    }

                    quoted = false;
                    index++;
                    continue;
                }

                cell.Append(current);
                index++;
                continue;
            }

            if (current == '"' && cell.Length == 0)
            {
                quoted = true;
                index++;
                continue;
            }

            if (current == separator)
            {
                EndCell();
                index++;
                continue;
            }

            if (current is '\r' or '\n')
            {
                EndRow();

                // CRLF ist ein Umbruch, nicht zwei.
                index += current == '\r' && index + 1 < content.Length && content[index + 1] == '\n' ? 2 : 1;
                continue;
            }

            cell.Append(current);
            index++;
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            EndRow();
        }

        return rows;
    }

    /// <summary>
    /// Setzt eine Zelle in Anführungszeichen, wo es nötig ist. Führende oder abschließende
    /// Leerzeichen zählen dazu — sonst verlöre der Wert sie beim nächsten Lesen.
    /// </summary>
    private static string Quote(string? cell, char separator)
    {
        if (string.IsNullOrEmpty(cell))
        {
            return string.Empty;
        }

        var needsQuotes = cell.Contains(separator)
            || cell.Contains('"')
            || cell.Contains('\n')
            || cell.Contains('\r')
            || cell[0] == ' '
            || cell[^1] == ' ';

        return needsQuotes ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
    }

    private static string FirstLine(string content)
    {
        var end = content.IndexOfAny(['\r', '\n']);
        return end < 0 ? content : content[..end];
    }

    private static int CountOutsideQuotes(string line, char needle)
    {
        var quoted = false;
        var count = 0;

        foreach (var current in line)
        {
            if (current == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && current == needle)
            {
                count++;
            }
        }

        return count;
    }
}
