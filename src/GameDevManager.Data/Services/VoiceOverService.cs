using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Eine Aufnahme zu einer Zeile in einer Sprache — oder deren Fehlen.</summary>
public sealed record VoiceOverTake(
    string LanguageCode, Guid? AssetId, string? FileName, string? VoiceActor, long SizeBytes)
{
    public bool IsRecorded => AssetId is not null;
}

/// <summary>Eine Dialogzeile mit ihren Aufnahmen, eine je Sprache des Projekts.</summary>
public sealed record VoiceOverLine(
    Guid LineId,
    string Text,
    string SpeakerName,
    int SortOrder,
    IReadOnlyList<VoiceOverTake> Takes);

/// <summary>Wie weit eine Sprache vertont ist.</summary>
public sealed record VoiceOverProgress(
    string LanguageCode, string LanguageName, int Total, int Recorded)
{
    public int Missing => Total - Recorded;

    public int Percent => Total == 0 ? 100 : (int)Math.Round(100d * Recorded / Total);
}

/// <summary>Die Vertonungsübersicht eines Dialogs.</summary>
public sealed record VoiceOverOverview(
    Guid DialogueId,
    string DialogueName,
    IReadOnlyList<ContentLanguage> Languages,
    IReadOnlyList<VoiceOverLine> Lines,
    IReadOnlyList<VoiceOverProgress> Progress);

/// <summary>
/// Die Vertonung der Dialogzeilen: welche Zeile in welcher Sprache eingesprochen ist, von wem,
/// und was noch fehlt.
/// <para>
/// <b>Kein eigener Datenbestand.</b> Eine Aufnahme ist ein <see cref="Asset"/>, das über
/// <see cref="Asset.OwnerEntityId"/> an der GUID der Zeile hängt — genau die Anbindung, für die
/// Assets gebaut sind und die das Cutscene-Storyboard schon benutzt. Dazugekommen sind nur zwei
/// Spalten am Asset: <see cref="Asset.LanguageCode"/> und <see cref="Asset.VoiceActor"/>. Eine
/// eigene Vertonungs-Tabelle hätte die Zuordnung Zeile→Datei ein zweites Mal geführt.
/// </para>
/// </summary>
public class VoiceOverService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Die Vertonungsübersicht eines Dialogs: je Zeile eine Zelle je Sprache.
    /// <c>null</c>, wenn es den Dialog nicht (mehr) gibt.
    /// <para>
    /// Aufgenommen wird in <b>allen</b> Sprachen des Projekts, die Ausgangssprache
    /// eingeschlossen — anders als bei den Übersetzungen, wo sie keine Zeile hat: Ihr Text
    /// steht ohnehin am Inhalt, ihre Aufnahme aber nirgends.
    /// </para>
    /// </summary>
    public async Task<VoiceOverOverview?> GetOverviewAsync(
        Guid projectId, Guid dialogueId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var dialogue = await db.Dialogues
            .AsNoTracking()
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == dialogueId && d.GameProjectId == projectId, ct);

        if (dialogue is null)
        {
            return null;
        }

        var languages = await db.ContentLanguages
            .AsNoTracking()
            .Where(language => language.GameProjectId == projectId)
            .OrderByDescending(language => language.IsSource)
            .ThenBy(language => language.SortOrder)
            .ThenBy(language => language.Name)
            .ToListAsync(ct);

        var lineIds = dialogue.Lines.Select(line => line.Id).ToList();

        var recordings = await db.Assets
            .AsNoTracking()
            .Where(asset => asset.OwnerModuleKey == ModuleKeys.Dialogs
                && asset.OwnerEntityId != null
                && lineIds.Contains(asset.OwnerEntityId!.Value)
                && asset.LanguageCode != null)
            .ToListAsync(ct);

        var speakerNames = await ResolveSpeakersAsync(db, dialogue, ct);

        var lines = dialogue.Lines
            .OrderBy(line => line.SortOrder)
            .Select(line => new VoiceOverLine(
                line.Id,
                line.Text,
                line.SpeakerNpcId is { } npcId
                    ? speakerNames.GetValueOrDefault(npcId, messages["VoiceOverUnknownSpeaker"].Value)
                    : messages["VoiceOverPlayerSpeaker"].Value,
                line.SortOrder,
                [.. languages.Select(language => BuildTake(recordings, line.Id, language.Code))]))
            .ToList();

        var progress = languages
            .Select(language => new VoiceOverProgress(
                language.Code,
                language.Name,
                lines.Count,
                lines.Count(line => line.Takes.Any(take => take.LanguageCode == language.Code && take.IsRecorded))))
            .ToList();

        return new VoiceOverOverview(dialogue.Id, dialogue.Name, languages, lines, progress);
    }

    private static VoiceOverTake BuildTake(List<Asset> recordings, Guid lineId, string languageCode)
    {
        var asset = recordings.FirstOrDefault(a =>
            a.OwnerEntityId == lineId
            && string.Equals(a.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));

        return asset is null
            ? new VoiceOverTake(languageCode, null, null, null, 0)
            : new VoiceOverTake(languageCode, asset.Id, asset.FileName, asset.VoiceActor, asset.SizeBytes);
    }

    private static async Task<Dictionary<Guid, string>> ResolveSpeakersAsync(
        GameDevManagerDbContext db, Dialogue dialogue, CancellationToken ct)
    {
        var npcIds = dialogue.Lines
            .Select(line => line.SpeakerNpcId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (npcIds.Count == 0)
        {
            return [];
        }

        return await db.Npcs
            .AsNoTracking()
            .Where(npc => npcIds.Contains(npc.Id))
            .ToDictionaryAsync(npc => npc.Id, npc => npc.Name, ct);
    }

    /// <summary>
    /// Hängt eine hochgeladene Datei als Aufnahme an eine Zeile: Sprache und Sprecher werden
    /// gesetzt, eine vorhandene Aufnahme derselben Sprache wird ersetzt.
    /// <para>
    /// Ersetzt heißt hier <b>gelöscht und neu</b> und nicht <c>AssetService.ReplaceAsync</c>:
    /// Dort bleibt die GUID, weil Verweise daran hängen — an einer Aufnahme hängt keiner, sie
    /// wird über Zeile und Sprache gefunden. Und es ist eine neue Aufnahme, keine neue Fassung
    /// derselben.
    /// </para>
    /// </summary>
    public async Task SetRecordingAsync(
        Guid assetId, Guid lineId, string languageCode, string? voiceActor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ContentValidationException(messages["VoiceOverLanguageRequired"]);
        }

        languageCode = languageCode.Trim();

        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new ContentValidationException(messages["AssetGone"]);

        var line = await db.DialogueLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lineId, ct)
            ?? throw new ContentValidationException(messages["VoiceOverLineGone"]);

        var known = await db.ContentLanguages
            .AnyAsync(language => language.GameProjectId == asset.GameProjectId
                && language.Code == languageCode, ct);

        if (!known)
        {
            throw new ContentValidationException(messages["VoiceOverLanguageUnknown", languageCode]);
        }

        var previous = await db.Assets
            .Where(a => a.OwnerEntityId == line.Id
                && a.OwnerModuleKey == ModuleKeys.Dialogs
                && a.LanguageCode == languageCode
                && a.Id != assetId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        asset.OwnerModuleKey = ModuleKeys.Dialogs;
        asset.OwnerEntityId = line.Id;
        asset.LanguageCode = languageCode;
        asset.VoiceActor = string.IsNullOrWhiteSpace(voiceActor) ? null : voiceActor.Trim();

        // Eine Aufnahme ist kein Icon; das Sprite-Nachrücken der Bibliothek soll sie nicht
        // zum primären Bild einer Zeile machen.
        asset.IsPrimary = false;

        await db.SaveChangesAsync(ct);

        foreach (var id in previous)
        {
            await assets.DeleteAsync(id, ct);
        }
    }

    /// <summary>Schreibt nur den Sprecher einer vorhandenen Aufnahme fort.</summary>
    public async Task SetVoiceActorAsync(Guid assetId, string? voiceActor, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new ContentValidationException(messages["AssetGone"]);

        asset.VoiceActor = string.IsNullOrWhiteSpace(voiceActor) ? null : voiceActor.Trim();
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------------ Health Check

    /// <summary>
    /// Der Health Check zur Vertonung: Zeilen, deren Text in einer Sprache <b>vorliegt</b>, für
    /// die aber keine Aufnahme existiert.
    /// <para>
    /// Gefragt wird nur nach Sprachen, in denen der Text auch wirklich dasteht — die
    /// Ausgangssprache immer, eine Zielsprache nur mit Übersetzung. Sonst meldete der Check
    /// beim Anlegen der zweiten Sprache auf einen Schlag jede Zeile des Projekts, und das ist
    /// keine Auskunft, sondern eine Wand.
    /// </para>
    /// <para>
    /// Ohne Sprachen im Projekt findet er nichts: Wer keine Lokalisierung pflegt, plant auch
    /// keine Vertonung — und eine Meldung „nichts vertont“ an jedem Dialog wäre Rauschen.
    /// </para>
    /// </summary>
    public async Task<List<VoiceOverGap>> FindMissingRecordingsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var languages = await db.ContentLanguages
            .AsNoTracking()
            .Where(language => language.GameProjectId == projectId)
            .OrderByDescending(language => language.IsSource)
            .ThenBy(language => language.SortOrder)
            .ToListAsync(ct);

        if (languages.Count == 0)
        {
            return [];
        }

        var dialogues = await db.Dialogues
            .AsNoTracking()
            .Include(d => d.Lines)
            .Where(d => d.GameProjectId == projectId)
            .ToListAsync(ct);

        var lineIds = dialogues.SelectMany(d => d.Lines).Select(line => line.Id).ToList();

        if (lineIds.Count == 0)
        {
            return [];
        }

        var recorded = await db.Assets
            .AsNoTracking()
            .Where(asset => asset.OwnerModuleKey == ModuleKeys.Dialogs
                && asset.OwnerEntityId != null
                && lineIds.Contains(asset.OwnerEntityId!.Value)
                && asset.LanguageCode != null)
            .Select(asset => new { LineId = asset.OwnerEntityId!.Value, asset.LanguageCode })
            .ToListAsync(ct);

        var recordedPairs = recorded
            .Select(entry => (entry.LineId, Code: entry.LanguageCode!.ToLowerInvariant()))
            .ToHashSet();

        // Die Übersetzungen der Zeilen — Slot „text“ an der GUID der Zeile, so wie F8 sie ablegt.
        var translated = await db.ContentTranslations
            .AsNoTracking()
            .Where(translation => lineIds.Contains(translation.OwnerEntityId)
                && translation.Slot == TranslationSlots.Text
                && translation.Text != "")
            .Select(translation => new { translation.OwnerEntityId, translation.LanguageCode })
            .ToListAsync(ct);

        var translatedPairs = translated
            .Select(entry => (entry.OwnerEntityId, Code: entry.LanguageCode.ToLowerInvariant()))
            .ToHashSet();

        var gaps = new List<VoiceOverGap>();

        foreach (var dialogue in dialogues.OrderBy(d => d.Name))
        {
            foreach (var line in dialogue.Lines.OrderBy(line => line.SortOrder))
            {
                foreach (var language in languages)
                {
                    var code = language.Code.ToLowerInvariant();

                    var hasText = language.IsSource || translatedPairs.Contains((line.Id, code));

                    if (hasText && !recordedPairs.Contains((line.Id, code)))
                    {
                        gaps.Add(new VoiceOverGap(
                            dialogue.Id, dialogue.Name, line.Id, line.Text, language.Code, language.Name));
                    }
                }
            }
        }

        return gaps;
    }
}

/// <summary>Eine Zeile, deren Text in einer Sprache vorliegt, ohne dass sie eingesprochen wäre.</summary>
public sealed record VoiceOverGap(
    Guid DialogueId,
    string DialogueName,
    Guid LineId,
    string LineText,
    string LanguageCode,
    string LanguageName);
