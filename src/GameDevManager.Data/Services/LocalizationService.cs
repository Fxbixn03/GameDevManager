using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Ein übersetzbarer Text einer Entität samt Ausgangsfassung und Übersetzung.</summary>
public sealed record TranslationRow(
    Guid OwnerEntityId,
    string OwnerModuleKey,
    string OwnerName,
    string Slot,
    string SlotLabel,
    string SourceText,
    string? Text,
    string? TranslatedFrom)
{
    /// <summary>Noch gar nicht übersetzt.</summary>
    public bool IsMissing => string.IsNullOrWhiteSpace(Text);

    /// <summary>
    /// Übersetzt, aber das Original hat sich seitdem geändert. Der wichtigere der beiden
    /// Zustände: Was fehlt, sieht man; was still falsch ist, nicht.
    /// </summary>
    public bool IsStale =>
        !IsMissing && TranslatedFrom is not null && !string.Equals(TranslatedFrom, SourceText, StringComparison.Ordinal);
}

/// <summary>Wie weit eine Sprache ist — die Antwort auf „was ist noch unübersetzt?“.</summary>
public sealed record TranslationProgress(
    string LanguageCode, string LanguageName, int Total, int Translated, int Stale)
{
    public int Missing => Total - Translated;

    public int Percent => Total == 0 ? 100 : (int)Math.Round(100d * (Translated - Stale) / Total);
}

/// <summary>
/// Die Lokalisierung der Spielinhalte: Item-Namen, Beschreibungen, Dialog- und Quest-Texte in
/// mehreren Sprachen.
/// <para>
/// Die <b>Ausgangssprache</b> steht dort, wo der Inhalt ohnehin steht — im Namen der Entität,
/// ihrer Beschreibung und ihren Textfeldern. Übersetzt wird daneben
/// (<see cref="ContentTranslation"/>), adressiert über die GUID des Besitzers wie Feldwerte und
/// Bedingungen. Ein Projekt mit nur einer Sprache zahlt damit nichts für die Mehrsprachigkeit.
/// </para>
/// <para>
/// Übersetzbar sind Name und Beschreibung jeder Entität sowie Feldwerte vom Typ Text und
/// mehrzeiliger Text — Zahlen, Schalter und Referenzen sind in jeder Sprache dieselben.
/// Stichwortlisten bleiben ebenfalls draußen: Sie sind Schlüssel für die Spiellogik
/// („Feuer“, „Eis“), keine Anzeigetexte.
/// </para>
/// </summary>
public class LocalizationService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>Die Feldtypen, deren Werte überhaupt übersetzt werden.</summary>
    public static bool IsTranslatable(FieldDefinition field) =>
        field.Type is ContentFieldType.Text or ContentFieldType.MultilineText && !field.IsKeywordField;

    // ------------------------------------------------------------------------- Sprachen

    public async Task<List<ContentLanguage>> GetLanguagesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentLanguages
            .AsNoTracking()
            .Where(language => language.GameProjectId == projectId)
            .OrderByDescending(language => language.IsSource)
            .ThenBy(language => language.SortOrder)
            .ThenBy(language => language.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Legt eine Sprache an oder ändert sie. Die erste Sprache eines Projekts wird immer die
    /// Ausgangssprache — ohne sie hätten die vorhandenen Inhalte kein Kürzel.
    /// </summary>
    public async Task SaveLanguageAsync(
        Guid projectId, ContentLanguage language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(language.Code))
        {
            throw new ContentValidationException(messages["Locale_CodeRequired"].Value);
        }

        if (string.IsNullOrWhiteSpace(language.Name))
        {
            throw new ContentValidationException(messages["Locale_NameRequired"].Value);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var code = language.Code.Trim();
        var taken = await db.ContentLanguages.AnyAsync(
            other => other.GameProjectId == projectId && other.Code == code && other.Id != language.Id, ct);

        if (taken)
        {
            throw new ContentValidationException(messages["Locale_CodeExists", code].Value);
        }

        var stored = await db.ContentLanguages.FirstOrDefaultAsync(l => l.Id == language.Id, ct);
        var isFirst = !await db.ContentLanguages.AnyAsync(l => l.GameProjectId == projectId, ct);

        if (stored is null)
        {
            stored = new ContentLanguage
            {
                Id = language.Id,
                GameProjectId = projectId,
                Code = code,
                Name = language.Name.Trim(),
                IsSource = isFirst || language.IsSource,
                SortOrder = language.SortOrder
            };

            db.ContentLanguages.Add(stored);
        }
        else
        {
            stored.Code = code;
            stored.Name = language.Name.Trim();
            stored.IsSource = language.IsSource;
            stored.SortOrder = language.SortOrder;
        }

        // Genau eine Ausgangssprache: Zwei wären ein Widerspruch, keine ließe die vorhandenen
        // Inhalte ohne Kürzel dastehen.
        if (stored.IsSource)
        {
            await db.ContentLanguages
                .Where(other => other.GameProjectId == projectId && other.Id != stored.Id && other.IsSource)
                .ForEachAsync(other => other.IsSource = false, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Entfernt eine Sprache samt ihrer Übersetzungen. Die Ausgangssprache lässt sich nicht
    /// löschen — mit ihr verschwände die Bedeutung aller vorhandenen Texte.
    /// </summary>
    public async Task DeleteLanguageAsync(Guid languageId, CancellationToken ct = default)
    {
        // Reiner ExecuteDelete-Pfad ohne vorheriges Speichern — die Prüfung steht hier.
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        var language = await db.ContentLanguages.FirstOrDefaultAsync(l => l.Id == languageId, ct);
        if (language is null)
        {
            return;
        }

        if (language.IsSource)
        {
            throw new ContentValidationException(messages["Locale_SourceUndeletable"].Value);
        }

        await db.ContentTranslations
            .Where(t => t.GameProjectId == language.GameProjectId && t.LanguageCode == language.Code)
            .ExecuteDeleteAsync(ct);

        await db.ContentLanguages.Where(l => l.Id == languageId).ExecuteDeleteAsync(ct);
    }

    // --------------------------------------------------------------------- Übersetzungen

    /// <summary>
    /// Alle übersetzbaren Texte eines Moduls in einer Sprache — die Arbeitsliste. Sortiert nach
    /// Entität und Slot, damit Name und Beschreibung derselben Entität beieinanderstehen.
    /// </summary>
    public async Task<List<TranslationRow>> GetRowsAsync(
        Guid projectId, string moduleKey, string languageCode, CancellationToken ct = default)
    {
        var source = sources.FirstOrDefault(entry => entry.ModuleKey == moduleKey)
            ?? throw new ContentValidationException(messages["Bulk_ModuleUnknown", moduleKey].Value);

        await using var db = await factory.CreateDbContextAsync(ct);

        var entities = await source.LoadAllAsync(db, projectId, ct);

        // Die zusätzlichen Texte des Moduls hängen an eigenen GUIDs — sie stehen also auch
        // dann da, wenn die Entität selbst nichts Übersetzbares mehr hätte.
        var extra = await source.GetTranslatableTextsAsync(db, projectId, ct);

        if (entities.Count == 0 && extra.Count == 0)
        {
            return [];
        }

        var ids = entities.Select(entity => entity.Id).ToList();

        var fields = await db.FieldDefinitions
            .AsNoTracking()
            .Where(field => field.ModuleKey == moduleKey)
            .ToListAsync(ct);

        var translatable = fields.Where(IsTranslatable).ToDictionary(field => field.Id);

        var values = await db.FieldValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.OwnerEntityId) && value.TextValue != null)
            .ToListAsync(ct);

        // Über beide Mengen zusammen: Die Teilobjekte tragen ihre Übersetzung unter ihrer
        // eigenen GUID, und die steht in keiner Entitätenliste.
        var owners = ids.Concat(extra.Select(text => text.OwnerEntityId)).Distinct().ToList();

        var stored = await db.ContentTranslations
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId
                && t.LanguageCode == languageCode
                && owners.Contains(t.OwnerEntityId))
            .ToListAsync(ct);

        var byKey = stored.ToDictionary(t => (t.OwnerEntityId, t.Slot));
        var rows = new List<TranslationRow>();

        // Die Zusatztexte stehen bei ihrer Entität und nicht hinten am Stück: Wer einen Dialog
        // übersetzt, will Name, Beschreibung und die Zeilen daran beieinander haben.
        var extraByEntity = extra
            .GroupBy(text => text.EntityId)
            .ToDictionary(group => group.Key, group => group.ToList());

        TranslationRow Row(Guid ownerId, string ownerName, string slot, string label, string text)
        {
            var translation = byKey.GetValueOrDefault((ownerId, slot));

            return new TranslationRow(
                ownerId, moduleKey, ownerName, slot, label, text,
                translation?.Text, translation?.SourceText);
        }

        foreach (var entity in entities)
        {
            rows.Add(Row(entity.Id, entity.Name, TranslationSlots.Name, TranslationSlots.Name, entity.Name));

            if (!string.IsNullOrWhiteSpace(entity.Description))
            {
                rows.Add(Row(
                    entity.Id, entity.Name, TranslationSlots.Description,
                    TranslationSlots.Description, entity.Description));
            }

            foreach (var value in values.Where(value => value.OwnerEntityId == entity.Id))
            {
                if (!translatable.TryGetValue(value.FieldDefinitionId, out var field)
                    || string.IsNullOrWhiteSpace(value.TextValue))
                {
                    continue;
                }

                rows.Add(Row(
                    entity.Id, entity.Name, TranslationSlots.ForField(field.Id), field.Name, value.TextValue));
            }

            if (extraByEntity.Remove(entity.Id, out var own))
            {
                rows.AddRange(own.Select(text =>
                    Row(text.OwnerEntityId, text.EntityName, text.Slot, text.SlotLabel, text.Text)));
            }
        }

        // Was keiner geladenen Entität zuzuordnen war, geht trotzdem nicht verloren.
        rows.AddRange(extraByEntity.Values
            .SelectMany(texts => texts)
            .Select(text => Row(text.OwnerEntityId, text.EntityName, text.Slot, text.SlotLabel, text.Text)));

        return rows;
    }

    /// <summary>
    /// Schreibt eine Übersetzung. Ein leerer Text löscht sie — „nicht übersetzt“ soll keine
    /// Zeile hinterlassen, dieselbe Regel wie bei Feldwerten und Bedingungssätzen.
    /// </summary>
    public async Task SaveAsync(
        Guid projectId, Guid ownerEntityId, string ownerModuleKey, string slot,
        string languageCode, string? text, string sourceText, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var known = await db.ContentLanguages.AnyAsync(
            language => language.GameProjectId == projectId && language.Code == languageCode, ct);

        if (!known)
        {
            throw new ContentValidationException(messages["Locale_LanguageUnknown", languageCode].Value);
        }

        var stored = await db.ContentTranslations.FirstOrDefaultAsync(
            t => t.OwnerEntityId == ownerEntityId && t.Slot == slot && t.LanguageCode == languageCode, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            if (stored is not null)
            {
                db.ContentTranslations.Remove(stored);
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        if (stored is null)
        {
            db.ContentTranslations.Add(new ContentTranslation
            {
                GameProjectId = projectId,
                OwnerEntityId = ownerEntityId,
                OwnerModuleKey = ownerModuleKey,
                Slot = slot,
                LanguageCode = languageCode,
                Text = text.Trim(),
                SourceText = sourceText
            });
        }
        else
        {
            stored.Text = text.Trim();
            stored.SourceText = sourceText;
            stored.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------------ Fortschritt

    /// <summary>
    /// Wie weit jede Sprache ist. Gezählt wird über alle Module — die Zahl beantwortet „was
    /// ist noch unübersetzt“, und die trennt man beim Planen nicht nach Modul.
    /// </summary>
    public async Task<List<TranslationProgress>> GetProgressAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var languages = await GetLanguagesAsync(projectId, ct);
        var targets = languages.Where(language => !language.IsSource).ToList();

        if (targets.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var total = 0;
        var sourceTexts = new Dictionary<(Guid, string), string>();

        foreach (var source in sources)
        {
            var entities = await source.LoadAllAsync(db, projectId, ct);

            foreach (var text in await source.GetTranslatableTextsAsync(db, projectId, ct))
            {
                sourceTexts[(text.OwnerEntityId, text.Slot)] = text.Text;
                total++;
            }

            if (entities.Count == 0)
            {
                continue;
            }

            var ids = entities.Select(entity => entity.Id).ToList();

            var translatable = (await db.FieldDefinitions
                    .AsNoTracking()
                    .Where(field => field.ModuleKey == source.ModuleKey)
                    .ToListAsync(ct))
                .Where(IsTranslatable)
                .Select(field => field.Id)
                .ToHashSet();

            var values = await db.FieldValues
                .AsNoTracking()
                .Where(value => ids.Contains(value.OwnerEntityId) && value.TextValue != null)
                .ToListAsync(ct);

            foreach (var entity in entities)
            {
                sourceTexts[(entity.Id, TranslationSlots.Name)] = entity.Name;
                total++;

                if (!string.IsNullOrWhiteSpace(entity.Description))
                {
                    sourceTexts[(entity.Id, TranslationSlots.Description)] = entity.Description;
                    total++;
                }
            }

            foreach (var value in values)
            {
                if (!translatable.Contains(value.FieldDefinitionId) || string.IsNullOrWhiteSpace(value.TextValue))
                {
                    continue;
                }

                sourceTexts[(value.OwnerEntityId, TranslationSlots.ForField(value.FieldDefinitionId))] = value.TextValue;
                total++;
            }
        }

        var stored = await db.ContentTranslations
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .Select(t => new { t.LanguageCode, t.OwnerEntityId, t.Slot, t.SourceText })
            .ToListAsync(ct);

        return
        [
            .. targets.Select(language =>
            {
                var mine = stored.Where(t => t.LanguageCode == language.Code).ToList();

                // Gezählt wird nur, was es auch noch gibt: Eine Übersetzung zu einem
                // gelöschten Text machte den Fortschritt größer als das Ganze.
                var live = mine
                    .Where(t => sourceTexts.ContainsKey((t.OwnerEntityId, t.Slot)))
                    .ToList();

                var stale = live.Count(t =>
                    t.SourceText is not null
                    && !string.Equals(t.SourceText, sourceTexts[(t.OwnerEntityId, t.Slot)], StringComparison.Ordinal));

                return new TranslationProgress(language.Code, language.Name, total, live.Count, stale);
            })
        ];
    }
}
