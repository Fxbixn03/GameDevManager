using System.Text;
using System.Text.RegularExpressions;

namespace GameDevManager.Data.Services;

/// <summary>
/// Maschinelle Übersetzungsvorschläge — die Schnittstelle, die das Übersetzungsraster fragt.
/// Der Anbieter (DeepL als erster) sitzt in der Web-Schicht, wie beim Mailversand: eine
/// ausgehende Verbindung gehört nicht neben die Datenbankzugriffe, und ein zweiter Anbieter
/// ist nur eine weitere Klasse hinter derselben Naht.
/// <para>
/// Ein Vorschlag ist ein <b>Vorschlag</b>: Er füllt die Zelle, gespeichert wird erst durch
/// die bestehende Je-Zelle-Speicherung — die Schnittstelle schreibt nie selbst.
/// </para>
/// </summary>
public interface ITranslationSuggester
{
    /// <summary>Ohne Konfiguration erscheint der Knopf im Raster gar nicht erst.</summary>
    bool IsConfigured { get; }

    /// <summary>Der Name des Anbieters — er steht am Knopf, damit klar ist, wer übersetzt.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Ein Vorschlag für einen Text. Wirft eine <see cref="ContentValidationException"/> mit
    /// verständlicher Meldung, wenn der Anbieter ablehnt — Kontingent, Schlüssel, Netz.
    /// </summary>
    Task<string> SuggestAsync(
        string text, string? sourceLanguageCode, string targetLanguageCode, CancellationToken ct = default);
}

/// <summary>Die Vorgabe ohne Anbieter — dieselbe Bauart wie der <see cref="NullMailSender"/>.</summary>
public sealed class NullTranslationSuggester : ITranslationSuggester
{
    public bool IsConfigured => false;

    public string ProviderName => string.Empty;

    public Task<string> SuggestAsync(
        string text, string? sourceLanguageCode, string targetLanguageCode, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);
}

/// <summary>
/// Schützt, was eine maschinelle Übersetzung nicht anfassen darf: Erwähnungen
/// (<c>[[items:GUID|Name]]</c>) und Platzhalter (<c>{0}</c>, <c>{name}</c>). Beides wird als
/// XML-Tag <c>&lt;x&gt;</c> eingepackt und dem Anbieter als zu ignorierendes Tag gemeldet
/// (bei DeepL: <c>tag_handling=xml</c> + <c>ignore_tags=x</c>) — der offizielle Weg statt
/// erfundener Ersatzzeichen, die eine Übersetzung auch mal „mitübersetzt“.
/// <para>
/// Markdown-Auszeichnung (<c>**fett**</c>) bleibt als Text im Satz — sie überlebt die
/// Übersetzung als Zeichen; nur die beiden Formen mit fester Bedeutung werden versiegelt.
/// </para>
/// </summary>
public static partial class TranslationText
{
    /// <summary>Escaped den Text als XML und versiegelt die geschützten Spannen in <c>&lt;x&gt;</c>.</summary>
    public static string ToXml(string text)
    {
        var xml = new StringBuilder(text.Length + 16);
        var position = 0;

        foreach (Match match in ProtectedPattern().Matches(text))
        {
            xml.Append(Escape(text[position..match.Index]));
            xml.Append("<x>").Append(Escape(match.Value)).Append("</x>");
            position = match.Index + match.Length;
        }

        xml.Append(Escape(text[position..]));

        return xml.ToString();
    }

    /// <summary>Entfernt die Siegel und macht das Escaping rückgängig.</summary>
    public static string FromXml(string xml) =>
        Unescape(xml.Replace("<x>", string.Empty).Replace("</x>", string.Empty));

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Unescape(string text) =>
        text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

    [GeneratedRegex(@"\[\[[^\]]*\]\]|\{[^{}\r\n]*\}")]
    private static partial Regex ProtectedPattern();
}

/// <summary>
/// Bildet die Sprachkürzel des Projekts (<c>ContentLanguage.Code</c>, BCP-47-artig) auf die
/// Codes von DeepL ab. Regionale Zielvarianten, die DeepL kennt, bleiben erhalten
/// (<c>en-GB</c> → <c>EN-GB</c>); alles andere fällt auf die Hauptsprache zurück
/// (<c>de-AT</c> → <c>DE</c>). Für <c>en</c> und <c>pt</c> als <b>Ziel</b> verlangt DeepL
/// eine Variante — der Rückfall wählt <c>EN-US</c> bzw. <c>PT-PT</c>.
/// </summary>
public static class DeepLLanguageMap
{
    private static readonly HashSet<string> RegionalTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "EN-GB", "EN-US", "PT-BR", "PT-PT", "ZH-HANS", "ZH-HANT"
    };

    public static string Map(string code, bool isTarget)
    {
        var trimmed = code.Trim().ToUpperInvariant();

        if (trimmed.Contains('-') && !(isTarget && RegionalTargets.Contains(trimmed)))
        {
            trimmed = trimmed[..trimmed.IndexOf('-')];
        }

        return isTarget switch
        {
            true when trimmed == "EN" => "EN-US",
            true when trimmed == "PT" => "PT-PT",
            _ => trimmed
        };
    }
}
