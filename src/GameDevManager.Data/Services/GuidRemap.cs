using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GameDevManager.Data.Services;

/// <summary>
/// Vergibt in einem JSON-Stand neue GUIDs — die gemeinsame Grundlage des Duplizierens, für
/// ein ganzes Projekt (<see cref="ProjectDuplication"/>) wie für eine einzelne Entität
/// (<see cref="EntityDuplication"/>).
/// <para>
/// Der Trick ist derselbe wie das Prinzip des Konzepts: Entitäten verweisen ausnahmslos über
/// GUIDs aufeinander, und die stehen in den Exportdateien genau so. Wer weiß, welche GUIDs
/// neu vergeben werden, kann sie deshalb über den <b>gesamten Text</b> austauschen und trifft
/// damit jede Referenz — auch die Fremdschlüssel der Kind-Sammlungen, die Besitzer-GUIDs der
/// Feldwerte und die Ziele der Bedingungen. Ein Verzeichnis der Spalten, in denen GUIDs
/// vorkommen können, müsste dagegen bei jedem neuen Modul nachgeführt werden.
/// </para>
/// </summary>
internal static partial class GuidRemap
{
    /// <summary>
    /// Trägt jede <c>id</c>-Eigenschaft aus <paramref name="node"/> in die Zuordnung ein — auch
    /// die der eingebetteten Kind-Sammlungen, denn an deren GUIDs hängen Bedingungssätze.
    /// Mehrere Aufrufe ergänzen dieselbe Zuordnung; ein bereits vergebener Eintrag bleibt.
    /// <para>
    /// Nur <c>id</c>: Alles andere ist ein Verweis. Wird eine Entität mitkopiert, bekommt sie
    /// hier ihre neue GUID und jeder Verweis darauf folgt; zeigt ein Verweis nach außen, steht
    /// sein Ziel nicht in der Zuordnung und bleibt unangetastet — genau richtig, denn die Kopie
    /// soll auf dieselbe fremde Entität zeigen.
    /// </para>
    /// </summary>
    internal static void Collect(JsonNode? node, Dictionary<string, string> map)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (key, value) in json)
                {
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase)
                        && value is JsonValue single
                        && single.TryGetValue<string>(out var text)
                        && Guid.TryParse(text, out var id))
                    {
                        map.TryAdd(id.ToString(), Guid.NewGuid().ToString());
                    }

                    Collect(value, map);
                }

                break;

            case JsonArray array:
                foreach (var element in array)
                {
                    Collect(element, map);
                }

                break;
        }
    }

    /// <summary>
    /// Tauscht in einem Durchlauf jede bekannte GUID gegen ihre neue. Unbekannte bleiben
    /// stehen — ein Verweis auf etwas außerhalb des Kopierten wäre durch einen Tausch nicht
    /// richtiger.
    /// </summary>
    internal static string Apply(string content, Dictionary<string, string> map) =>
        GuidPattern().Replace(content, match =>
            map.TryGetValue(match.Value, out var replacement) ? replacement : match.Value);

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidPattern();
}
