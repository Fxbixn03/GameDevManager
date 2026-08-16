using System.Text;
using System.Text.RegularExpressions;

namespace GameDevManager.Domain.Entities;

/// <summary>Ein Stück gerenderten Textes — entweder Auszeichnung oder eine Erwähnung.</summary>
/// <param name="Html">Fertiges, bereits maskiertes HTML.</param>
/// <param name="Mention">Gesetzt, wenn dieses Stück eine Erwähnung ist — die Anzeige verlinkt sie.</param>
public sealed record MarkdownSegment(string Html, ContentMention? Mention);

/// <summary>
/// Ein kleiner Markdown-Satz für die Fließtexte des Hauses: Überschriften, fett, kursiv, Code,
/// Aufzählungen, Absätze — mehr nicht.
/// <para>
/// Selbst geschrieben statt als Fremdbibliothek gezogen, dieselbe Abwägung wie beim
/// <c>ImageDimensionReader</c>, beim <c>Csv</c> und beim Ausdrucksrechner der Kurven: Ein
/// Story-Text braucht keine Tabellen, keine Fußnoten und keine eingebetteten Bilder, und eine
/// Bibliothek dafür wäre mehr Abhängigkeit als Nutzen.
/// </para>
/// <para>
/// <b>Alles wird maskiert.</b> Der Text kommt aus einem Eingabefeld; eingebettetes HTML
/// gelangte sonst ungefiltert in die Seite — dieselbe Vorsicht wie beim Ausliefern
/// hochgeladener Dateien.
/// </para>
/// </summary>
public static partial class SimpleMarkdown
{
    /// <summary>
    /// Rendert einen Text in Blöcke. Jeder Block ist eine Liste von Stücken, damit die Anzeige
    /// die Erwähnungen als echte Verknüpfungen setzen kann statt als rohes HTML.
    /// </summary>
    public static List<List<MarkdownSegment>> Render(string? text)
    {
        var blocks = new List<List<MarkdownSegment>>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return blocks;
        }

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimEnd();

            // Eine Leerzeile trennt Absätze; zwei hintereinander bleiben eine Trennung.
            if (trimmed.Length == 0)
            {
                continue;
            }

            blocks.Add(RenderLine(trimmed));
        }

        return blocks;
    }

    /// <summary>Ob eine Zeile eine Überschrift ist und welcher Stufe — für die Anzeige.</summary>
    public static int HeadingLevel(string line)
    {
        var level = 0;

        while (level < line.Length && line[level] == '#' && level < 4)
        {
            level++;
        }

        return level > 0 && level < line.Length && line[level] == ' ' ? level : 0;
    }

    /// <summary>Ob eine Zeile ein Aufzählungspunkt ist.</summary>
    public static bool IsListItem(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal);

    /// <summary>Der Text einer Zeile ohne ihre Blockmarkierung.</summary>
    public static string StripMarker(string line)
    {
        var level = HeadingLevel(line);
        if (level > 0)
        {
            return line[(level + 1)..].Trim();
        }

        return IsListItem(line) ? line[2..].Trim() : line;
    }

    private static List<MarkdownSegment> RenderLine(string line)
    {
        var content = StripMarker(line);
        var segments = new List<MarkdownSegment>();
        var index = 0;

        // Erwähnungen zerteilen die Zeile; alles dazwischen bekommt die Inline-Auszeichnung.
        foreach (Match match in MentionPattern().Matches(content))
        {
            if (match.Index > index)
            {
                segments.Add(new MarkdownSegment(Inline(content[index..match.Index]), null));
            }

            var mention = ContentMentions.Parse(match.Value).FirstOrDefault();
            segments.Add(new MarkdownSegment(Escape(match.Groups[3].Value.Trim()), mention));

            index = match.Index + match.Length;
        }

        if (index < content.Length)
        {
            segments.Add(new MarkdownSegment(Inline(content[index..]), null));
        }

        return segments;
    }

    /// <summary>
    /// Fett, kursiv, Code und Links. Maskiert wird <b>zuerst</b>, ausgezeichnet danach — sonst
    /// verwandelte die Maskierung die eben gesetzten Tags wieder in Text.
    /// </summary>
    private static string Inline(string text)
    {
        var html = Escape(text);

        html = CodePattern().Replace(html, "<code>$1</code>");
        html = BoldPattern().Replace(html, "<b>$1</b>");
        html = ItalicPattern().Replace(html, "<i>$1</i>");

        // Nur http(s): Ein „javascript:“ im Ziel wäre genau der Weg, den die Maskierung
        // eigentlich versperrt.
        html = LinkPattern().Replace(html, match =>
            $"<a href=\"{match.Groups[2].Value}\" target=\"_blank\" rel=\"noopener noreferrer\">{match.Groups[1].Value}</a>");

        return html;
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => character.ToString()
            });
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\[\[([a-z0-9_-]+):([0-9a-fA-F-]{36})\|([^\]]*)\]\]")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial Regex ItalicPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^\s)]+)\)")]
    private static partial Regex LinkPattern();
}
