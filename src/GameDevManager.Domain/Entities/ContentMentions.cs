using System.Text.RegularExpressions;

namespace GameDevManager.Domain.Entities;

/// <summary>Eine Erwähnung in einem Fließtext: welche Entität, unter welchem Namen.</summary>
public sealed record ContentMention(string ModuleKey, Guid EntityId, string DisplayName);

/// <summary>
/// Erwähnungen in Fließtexten — der Story-Text, die Notiz einer Kanban-Karte, die Beschreibung
/// einer Entität.
/// <para>
/// Geschrieben wird <c>@Eisenschwert</c>; beim Speichern löst der Dienst den Namen auf und legt
/// <c>[[items:GUID|Eisenschwert]]</c> ab. Zwei Dinge stehen damit fest: Die <b>GUID</b> macht
/// den Verweis stabil gegen Umbenennungen, und der <b>Anzeigename</b> bleibt lesbar, wenn die
/// Entität verschwindet — dieselbe Überlegung wie beim Namen im Änderungsprotokoll.
/// </para>
/// <para>
/// Der <see cref="ReferenceService"/> findet die Erwähnungen über eine Textsuche nach der GUID,
/// genau wie bei den Referenzlisten in den Feldwerten. Es braucht dafür keine eigene Tabelle.
/// </para>
/// </summary>
public static partial class ContentMentions
{
    /// <summary>Baut die gespeicherte Form einer Erwähnung.</summary>
    public static string Format(string moduleKey, Guid entityId, string displayName) =>
        $"[[{moduleKey}:{entityId}|{Escape(displayName)}]]";

    /// <summary>Alle Erwähnungen eines Textes, in ihrer Reihenfolge.</summary>
    public static List<ContentMention> Parse(string? text)
    {
        var mentions = new List<ContentMention>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return mentions;
        }

        foreach (Match match in MentionPattern().Matches(text))
        {
            if (Guid.TryParse(match.Groups[2].Value, out var id))
            {
                mentions.Add(new ContentMention(match.Groups[1].Value, id, Unescape(match.Groups[3].Value)));
            }
        }

        return mentions;
    }

    /// <summary>
    /// Löst <c>@Name</c> gegen ein Verzeichnis auf und schreibt die gespeicherte Form. Was sich
    /// nicht auflösen lässt, bleibt <b>unverändert stehen</b>: Ein Text ist Text, und ein
    /// Tippfehler in einem Namen darf ihn nicht verstümmeln.
    /// <para>
    /// Der längste passende Name gewinnt — sonst schnappte sich „Eisen“ den Anfang von
    /// „Eisenschwert“ und ließe den Rest als losen Text zurück.
    /// </para>
    /// </summary>
    public static string? Resolve(string? text, IReadOnlyDictionary<string, ContentMention> byName)
    {
        if (string.IsNullOrWhiteSpace(text) || byName.Count == 0)
        {
            return text;
        }

        // Nach Länge absteigend: Der Regex-Wechsel nimmt die erste passende Alternative.
        var names = byName.Keys
            .OrderByDescending(name => name.Length)
            .Select(Regex.Escape);

        var pattern = $"@({string.Join('|', names)})";

        return Regex.Replace(
            text,
            pattern,
            match => byName.TryGetValue(match.Groups[1].Value, out var mention)
                ? Format(mention.ModuleKey, mention.EntityId, mention.DisplayName)
                : match.Value,
            RegexOptions.None,
            TimeSpan.FromMilliseconds(250));
    }

    /// <summary>
    /// Ersetzt die gespeicherte Form wieder durch <c>@Name</c> — die Fassung, die in der Maske
    /// steht. Ohne das sähe der Autor beim zweiten Öffnen seinen eigenen Text nicht wieder.
    /// </summary>
    public static string? ToEditable(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? text
            : MentionPattern().Replace(text, match => "@" + Unescape(match.Groups[3].Value));

    /// <summary>Ein Anzeigename darf die Klammern der Syntax nicht selbst enthalten.</summary>
    private static string Escape(string name) =>
        name.Replace("|", " ").Replace("[", " ").Replace("]", " ").Trim();

    private static string Unescape(string name) => name.Trim();

    [GeneratedRegex(@"\[\[([a-z0-9_-]+):([0-9a-fA-F-]{36})\|([^\]]*)\]\]")]
    private static partial Regex MentionPattern();
}
