using System.IO.Compression;
using System.Net;
using System.Text;
using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Zeile des Aufnahme-Skripts: wer spricht was, mit Kontext und Dateinamen-Vorgabe.</summary>
/// <param name="Text">Der zu sprechende Text — in der Zielsprache die Übersetzung, nicht das Original.</param>
/// <param name="PreviousText">Die Zeile davor als Kontext: Ein Sprecher braucht den Anschluss.</param>
/// <param name="FileName">Die Vorgabe für die Aufnahme-Datei — über sie findet der Rückweg die Zeile.</param>
public sealed record RecordingScriptLine(
    Guid LineId,
    string DialogueName,
    string Speaker,
    string Text,
    string? PreviousText,
    string FileName);

/// <summary>Was der ZIP-Rückweg getan hat — und was er ausdrücklich nicht geraten hat.</summary>
public sealed record RecordingImportResult(int Assigned, IReadOnlyList<string> Conflicts);

/// <summary>
/// Die Aufnahmeliste für das Tonstudio: alle offenen Zeilen einer Sprache als Skript (CSV und
/// druckbares HTML), je Zeile Sprecherrolle, Text, Kontext und eine Dateinamen-Vorgabe — und
/// der Rückweg als ZIP, dessen Dateien über genau diese Namen den Zeilen zugeordnet werden.
/// <para>
/// <b>Was „offen“ ist, sagt der Health Check</b> (<see cref="VoiceOverService.FindMissingRecordingsAsync"/>)
/// — er bleibt die Quelle der Wahrheit, das Skript ist nur seine Studio-Fassung. Die
/// Dateinamen-Vorgabe ist <c>&lt;zeilen-guid&gt;.&lt;sprachcode&gt;.wav</c>: Die GUID findet
/// die Zeile, der Code die Sprache, die Endung darf abweichen. Was sich nicht eindeutig
/// zuordnen lässt, wird als Konflikt gemeldet statt geraten — dieselbe Linie wie beim
/// Zuordnen der Assets nach Namen.
/// </para>
/// </summary>
public class RecordingListService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    VoiceOverService voiceOvers,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    // ------------------------------------------------------------------------------ Skript

    public async Task<List<RecordingScriptLine>> GetScriptAsync(
        Guid projectId, string languageCode, CancellationToken ct = default)
    {
        var code = languageCode.Trim();

        // Der Health Check bestimmt, was offen ist — das Skript formt es nur um.
        var gaps = (await voiceOvers.FindMissingRecordingsAsync(projectId, ct))
            .Where(gap => gap.LanguageCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gaps.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var dialogueIds = gaps.Select(gap => gap.DialogueId).Distinct().ToList();

        var dialogues = await db.Dialogues
            .AsNoTracking()
            .Include(dialogue => dialogue.Lines)
            .Where(dialogue => dialogueIds.Contains(dialogue.Id))
            .ToListAsync(ct);

        var lineIds = dialogues.SelectMany(dialogue => dialogue.Lines).Select(line => line.Id).ToList();

        // In der Zielsprache spricht das Studio die Übersetzung — Slot „text“, wie überall.
        var translations = (await db.ContentTranslations
                .AsNoTracking()
                .Where(translation => lineIds.Contains(translation.OwnerEntityId)
                    && translation.Slot == TranslationSlots.Text
                    && translation.LanguageCode == code)
                .ToListAsync(ct))
            .ToDictionary(translation => translation.OwnerEntityId, translation => translation.Text);

        var npcIds = dialogues
            .SelectMany(dialogue => dialogue.Lines)
            .Select(line => line.SpeakerNpcId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var speakers = await db.Npcs
            .AsNoTracking()
            .Where(npc => npcIds.Contains(npc.Id))
            .ToDictionaryAsync(npc => npc.Id, npc => npc.Name, ct);

        var openLineIds = gaps.Select(gap => gap.LineId).ToHashSet();
        var script = new List<RecordingScriptLine>();

        foreach (var dialogue in dialogues.OrderBy(dialogue => dialogue.Name))
        {
            var ordered = dialogue.Lines.OrderBy(line => line.SortOrder).ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                var line = ordered[index];

                if (!openLineIds.Contains(line.Id))
                {
                    continue;
                }

                var previous = index > 0 ? ordered[index - 1] : null;

                script.Add(new RecordingScriptLine(
                    line.Id,
                    dialogue.Name,
                    line.SpeakerNpcId is { } npcId
                        ? speakers.GetValueOrDefault(npcId, messages["VoiceOverUnknownSpeaker"].Value)
                        : messages["VoiceOverPlayerSpeaker"].Value,
                    translations.GetValueOrDefault(line.Id) ?? line.Text,
                    previous is null
                        ? null
                        : translations.GetValueOrDefault(previous.Id) ?? previous.Text,
                    BuildFileName(line.Id, code)));
            }
        }

        return script;
    }

    /// <summary>Die Vorgabe: GUID der Zeile, Sprachcode, Endung — die GUID trägt den Rückweg.</summary>
    public static string BuildFileName(Guid lineId, string languageCode) =>
        $"{lineId:N}.{languageCode.ToLowerInvariant()}.wav";

    /// <summary>Das Skript als Tabelle — dieselbe Datei, die auch das Studio pflegen kann.</summary>
    public static string BuildCsv(IReadOnlyList<RecordingScriptLine> script)
    {
        var rows = new List<string>
        {
            Csv.FormatRow(["datei", "dialog", "sprecher", "text", "kontext"])
        };

        rows.AddRange(script.Select(line => Csv.FormatRow(
            [line.FileName, line.DialogueName, line.Speaker, line.Text, line.PreviousText])));

        return string.Join("\r\n", rows);
    }

    /// <summary>
    /// Das Skript als eigenständige, druckbare HTML-Datei — dieselbe Linie wie das
    /// Design-Dokument: vollständig abgeleitet, kein eigenes Format, das PDF liefert der
    /// Browser-Druck.
    /// </summary>
    public string BuildHtml(string projectName, string languageName, IReadOnlyList<RecordingScriptLine> script)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        html.AppendLine($"<title>{Encode(messages["RecordingScriptTitle", projectName, languageName].Value)}</title>");
        html.AppendLine("""
            <style>
                body { font-family: system-ui, sans-serif; margin: 2rem; color: #1a1a1a; }
                h1 { font-size: 1.4rem; } h2 { font-size: 1.1rem; margin-top: 2rem; }
                table { border-collapse: collapse; width: 100%; }
                th, td { border: 1px solid #999; padding: 0.4rem 0.6rem; text-align: left; vertical-align: top; }
                th { background: #eee; }
                .file { font-family: monospace; font-size: 0.8rem; }
                .context { color: #555; font-size: 0.9rem; }
                @media print { h2 { break-after: avoid; } tr { break-inside: avoid; } }
            </style>
            """);
        html.AppendLine("</head><body>");
        html.AppendLine($"<h1>{Encode(messages["RecordingScriptTitle", projectName, languageName].Value)}</h1>");

        foreach (var dialogue in script.GroupBy(line => line.DialogueName))
        {
            html.AppendLine($"<h2>{Encode(dialogue.Key)}</h2>");
            html.AppendLine("<table><thead><tr>");
            html.AppendLine($"<th>{Encode(messages["RecordingScriptSpeaker"].Value)}</th>");
            html.AppendLine($"<th>{Encode(messages["RecordingScriptText"].Value)}</th>");
            html.AppendLine($"<th>{Encode(messages["RecordingScriptContext"].Value)}</th>");
            html.AppendLine($"<th>{Encode(messages["RecordingScriptFile"].Value)}</th>");
            html.AppendLine("</tr></thead><tbody>");

            foreach (var line in dialogue)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{Encode(line.Speaker)}</td>");
                html.AppendLine($"<td>{Encode(line.Text)}</td>");
                html.AppendLine($"<td class=\"context\">{Encode(line.PreviousText)}</td>");
                html.AppendLine($"<td class=\"file\">{Encode(line.FileName)}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody></table>");
        }

        html.AppendLine("</body></html>");

        return html.ToString();
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    // ---------------------------------------------------------------------------- Rückweg

    /// <summary>
    /// Liest ein ZIP mit benannten Aufnahmen zurück. Zugeordnet wird über den vorgegebenen
    /// Dateinamen (<c>&lt;guid&gt;.&lt;code&gt;.&lt;endung&gt;</c>); eine vorhandene Aufnahme
    /// derselben Sprache wird ersetzt, wie in der Vertonungs-Matrix. Alles Unklare ist ein
    /// <b>Konflikt und wird gemeldet statt geraten</b> — die übrigen Dateien gehen trotzdem
    /// ihren Weg: Ein Tippfehler in einer Datei darf nicht vierzig richtige aufhalten.
    /// </summary>
    public async Task<RecordingImportResult> ImportZipAsync(
        Guid projectId, Stream zip, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);

        var assigned = 0;
        var conflicts = new List<string>();

        await using var db = await factory.CreateDbContextAsync(ct);

        var knownCodes = (await db.ContentLanguages
                .AsNoTracking()
                .Where(language => language.GameProjectId == projectId)
                .Select(language => language.Code)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            // Ordnereinträge und Metadaten von Packprogrammen sind keine Aufnahmen.
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            if (!TryParseFileName(entry.Name, out var lineId, out var code))
            {
                conflicts.Add(messages["RecordingZipBadName", entry.Name].Value);
                continue;
            }

            if (!knownCodes.Contains(code))
            {
                conflicts.Add(messages["RecordingZipUnknownLanguage", entry.Name, code].Value);
                continue;
            }

            var lineExists = await db.DialogueLines
                .AsNoTracking()
                .AnyAsync(line => line.Id == lineId
                    && line.Dialogue!.GameProjectId == projectId, ct);

            if (!lineExists)
            {
                conflicts.Add(messages["RecordingZipUnknownLine", entry.Name].Value);
                continue;
            }

            try
            {
                // Der Upload braucht einen spulbaren Strom — ZIP-Einträge sind keiner.
                using var buffer = new MemoryStream();
                await using (var content = entry.Open())
                {
                    await content.CopyToAsync(buffer, ct);
                }
                buffer.Position = 0;

                var asset = await assets.UploadAsync(
                    projectId, entry.Name, MimeTypeFor(entry.Name), buffer,
                    ModuleKeys.Dialogs, lineId, ct);

                await voiceOvers.SetRecordingAsync(asset.Id, lineId, code, voiceActor: null, ct);
                assigned++;
            }
            catch (ContentValidationException ex)
            {
                conflicts.Add($"{entry.Name}: {ex.Message}");
            }
        }

        return new RecordingImportResult(assigned, conflicts);
    }

    /// <summary>
    /// Zerlegt „&lt;guid&gt;.&lt;code&gt;.&lt;endung&gt;“. Die GUID darf mit oder ohne
    /// Bindestriche stehen — <see cref="Guid.TryParse(string?, out Guid)"/> nimmt beide.
    /// </summary>
    internal static bool TryParseFileName(string fileName, out Guid lineId, out string languageCode)
    {
        lineId = Guid.Empty;
        languageCode = string.Empty;

        var parts = Path.GetFileName(fileName).Split('.');

        if (parts.Length < 3 || !Guid.TryParse(parts[0], out lineId))
        {
            return false;
        }

        languageCode = parts[^2].Trim();

        return languageCode.Length > 0;
    }

    private static string MimeTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".ogg" => "audio/ogg",
        ".flac" => "audio/flac",
        ".m4a" => "audio/mp4",
        _ => "application/octet-stream"
    };
}
