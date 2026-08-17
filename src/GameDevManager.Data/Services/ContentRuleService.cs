using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>Ein Fund einer eigenen Prüfung.</summary>
public sealed record ContentRuleFinding(
    Guid RuleId,
    string RuleName,
    ContentRuleSeverity Severity,
    string ModuleKey,
    Guid EntityId,
    string EntityName);

/// <summary>Eine Regel samt der Zahl ihrer Funde.</summary>
public sealed record ContentRuleResult(ContentRule Rule, IReadOnlyList<ContentRuleFinding> Findings);

/// <summary>
/// Eigene Health-Check-Regeln (F18): Was die acht eingebauten Prüfungen nicht wissen können —
/// „jedes Item braucht ein Sprite“, „kein NPC ohne Art“, „jede Quest braucht eine
/// Freischaltbedingung“.
/// <para>
/// Ausgewertet wird über die <see cref="IModuleEntitySource"/> wie Suche, Referenzansicht und
/// die gespeicherten Ansichten — ein neues Modul ist von selbst prüfbar.
/// </para>
/// <para>
/// <b>Regelarten statt Skriptsprache.</b> Eine Handvoll deckt neunzig Prozent ab und lässt sich
/// in einer Maske erfassen. Was bewusst fehlt, ist „auf diese Entität zeigt nichts aus Modul
/// Y“: Verweise laufen teils über Feldwerte, teils über modul-eigene Spalten
/// (Fraktions-Mitglieder, Rezept-Zutaten), und die zweite Hälfte ließe sich nur je Entität
/// erfragen — bei dreihundert NPCs wären das tausende Abfragen. Ein Modul, das die Umkehrung
/// nicht überschriebe, meldete zudem still Fehlfunde, und eine Prüfung, der man nicht trauen
/// kann, ist schlechter als keine. Den wichtigsten Referenzfall deckt der eingebaute Check
/// „tote Items“ ab.
/// </para>
/// </summary>
public class ContentRuleService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    IEnumerable<IModuleEntitySource> sources,
    ContentTypeService contentTypes,
    PermissionGuard guard,
    IStringLocalizer<DataMessages> messages)
{
    // ------------------------------------------------------------------------- Verwalten

    public async Task<List<ContentRule>> GetRulesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.ContentRules
            .AsNoTracking()
            .Where(rule => rule.GameProjectId == projectId)
            .OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.Name)
            .ToListAsync(ct);
    }

    public async Task SaveRuleAsync(ContentRule rule, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ContentValidationException(messages["RuleNameRequired"]);
        }

        if (sources.All(source => source.ModuleKey != rule.ModuleKey))
        {
            throw new ContentValidationException(messages["RuleModuleUnknown", rule.ModuleKey]);
        }

        // Eine Regel ohne ihre Angabe prüfte nichts und stünde trotzdem als Zeile da.
        if (rule.Check == ContentRuleCheck.FieldEmpty && rule.FieldDefinitionId is null)
        {
            throw new ContentValidationException(messages["RuleFieldRequired"]);
        }

        if (rule.Check == ContentRuleCheck.NoConditions && string.IsNullOrWhiteSpace(rule.Slot))
        {
            throw new ContentValidationException(messages["RuleSlotRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.ContentRules.FirstOrDefaultAsync(r => r.Id == rule.Id, ct);

        if (stored is null)
        {
            stored = new ContentRule
            {
                Id = rule.Id,
                GameProjectId = rule.GameProjectId,
                Name = rule.Name.Trim(),
                ModuleKey = rule.ModuleKey,
                SortOrder = await db.ContentRules.CountAsync(r => r.GameProjectId == rule.GameProjectId, ct)
            };

            db.ContentRules.Add(stored);
        }

        stored.Name = rule.Name.Trim();
        stored.ModuleKey = rule.ModuleKey;
        stored.ContentTypeId = rule.ContentTypeId;
        stored.Check = rule.Check;
        stored.Severity = rule.Severity;
        stored.IsEnabled = rule.IsEnabled;

        // Angaben, die zur gewählten Regelart nicht gehören, werden geleert — sonst wirkten sie
        // beim Zurückwechseln unbemerkt weiter. Dieselbe Regel wie beim Feldtyp-Wechsel.
        stored.FieldDefinitionId = rule.Check == ContentRuleCheck.FieldEmpty ? rule.FieldDefinitionId : null;
        stored.TagId = rule.Check == ContentRuleCheck.NoTag ? rule.TagId : null;
        stored.Slot = rule.Check == ContentRuleCheck.NoConditions ? rule.Slot?.Trim() : null;

        await db.SaveChangesAsync(ct);

        rule.SortOrder = stored.SortOrder;
    }

    public async Task DeleteRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        await guard.EnsureCanWriteAsync(ct);

        await using var db = await factory.CreateDbContextAsync(ct);

        await db.ContentRules.Where(rule => rule.Id == ruleId).ExecuteDeleteAsync(ct);
    }

    // -------------------------------------------------------------------------- Auswerten

    /// <summary>
    /// Wertet alle eingeschalteten Regeln eines Projekts aus. Regeln ohne Fund stehen mit
    /// leerer Liste darin — sonst wäre nicht erkennbar, dass geprüft wurde; dieselbe Linie wie
    /// bei den eingebauten Health Checks.
    /// </summary>
    public async Task<List<ContentRuleResult>> EvaluateAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var rules = (await GetRulesAsync(projectId, ct)).Where(rule => rule.IsEnabled).ToList();

        if (rules.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var results = new List<ContentRuleResult>();

        foreach (var rule in rules)
        {
            var source = sources.FirstOrDefault(s => s.ModuleKey == rule.ModuleKey);

            if (source is null)
            {
                // Ein Modul, das es nicht mehr gibt — die Regel bleibt stehen und findet nichts.
                results.Add(new ContentRuleResult(rule, []));
                continue;
            }

            results.Add(new ContentRuleResult(rule, await EvaluateRuleAsync(db, projectId, rule, source, ct)));
        }

        return results;
    }

    private async Task<List<ContentRuleFinding>> EvaluateRuleAsync(
        GameDevManagerDbContext db, Guid projectId, ContentRule rule,
        IModuleEntitySource source, CancellationToken ct)
    {
        // Über denselben Filter wie die gespeicherten Ansichten: Die Art samt Unterarten
        // einzuschränken ist dort schon gelöst, und zwei Fassungen liefen auseinander.
        var filter = new ContentFilter { ContentTypeId = rule.ContentTypeId };

        if (rule.ContentTypeId is { } chosen)
        {
            filter.ExpandedTypeIds = await ExpandAsync(projectId, rule.ModuleKey, chosen, ct);
        }

        var candidates = await source.QueryAsync(db, projectId, filter, ct);

        if (candidates.Count == 0)
        {
            return [];
        }

        var ids = candidates.Select(candidate => candidate.Id).ToList();

        var offenders = rule.Check switch
        {
            ContentRuleCheck.FieldEmpty => await WithoutFieldValueAsync(db, ids, rule.FieldDefinitionId!.Value, ct),
            ContentRuleCheck.NoPrimarySprite => await WithoutPrimarySpriteAsync(db, ids, ct),
            ContentRuleCheck.NoDescription =>
                [.. candidates
                    .Where(candidate => string.IsNullOrWhiteSpace(candidate.Description))
                    .Select(candidate => candidate.Id)],
            ContentRuleCheck.NoTag => await WithoutTagAsync(db, ids, rule.TagId, ct),
            ContentRuleCheck.NoConditions => await WithoutConditionsAsync(db, ids, rule.Slot!, ct),
            ContentRuleCheck.NoContentType =>
                [.. candidates.Where(candidate => candidate.TypeName is null).Select(candidate => candidate.Id)],
            _ => []
        };

        var offending = offenders.ToHashSet();

        return
        [
            .. candidates
                .Where(candidate => offending.Contains(candidate.Id))
                .Select(candidate => new ContentRuleFinding(
                    rule.Id, rule.Name, rule.Severity, rule.ModuleKey, candidate.Id, candidate.Name))
        ];
    }

    private async Task<List<Guid>> ExpandAsync(
        Guid projectId, string moduleKey, Guid chosen, CancellationToken ct)
    {
        var types = await contentTypes.GetTypesAsync(projectId, moduleKey, ct);

        var wanted = new HashSet<Guid> { chosen };
        var grew = true;

        while (grew)
        {
            grew = false;

            foreach (var type in types.Where(type => type.ParentId is { } parent && wanted.Contains(parent)))
            {
                grew |= wanted.Add(type.Id);
            }
        }

        return [.. wanted];
    }

    private static async Task<List<Guid>> WithoutFieldValueAsync(
        GameDevManagerDbContext db, List<Guid> ids, Guid fieldId, CancellationToken ct)
    {
        var filled = await db.FieldValues
            .AsNoTracking()
            .Where(value => ids.Contains(value.OwnerEntityId) && value.FieldDefinitionId == fieldId)
            .ToListAsync(ct);

        // Über IsEmpty und nicht über „Zeile vorhanden“: Eine Stichwortliste aus lauter Kommas
        // trägt Text, aber keinen Wert — dieselbe Frage wie bei der Pflichtfeldprüfung.
        var withValue = filled.Where(value => !value.IsEmpty).Select(value => value.OwnerEntityId).ToHashSet();

        return [.. ids.Where(id => !withValue.Contains(id))];
    }

    private static async Task<List<Guid>> WithoutPrimarySpriteAsync(
        GameDevManagerDbContext db, List<Guid> ids, CancellationToken ct)
    {
        var withSprite = await db.Assets
            .AsNoTracking()
            .Where(asset => asset.OwnerEntityId != null
                && ids.Contains(asset.OwnerEntityId!.Value)
                && asset.IsPrimary)
            .Select(asset => asset.OwnerEntityId!.Value)
            .ToListAsync(ct);

        return [.. ids.Except(withSprite)];
    }

    private static async Task<List<Guid>> WithoutTagAsync(
        GameDevManagerDbContext db, List<Guid> ids, Guid? tagId, CancellationToken ct)
    {
        var tagged = await db.ContentTagAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.TargetEntityId)
                && (tagId == null || assignment.ContentTagId == tagId))
            .Select(assignment => assignment.TargetEntityId)
            .ToListAsync(ct);

        return [.. ids.Except(tagged)];
    }

    private static async Task<List<Guid>> WithoutConditionsAsync(
        GameDevManagerDbContext db, List<Guid> ids, string slot, CancellationToken ct)
    {
        var withSet = await db.ConditionSets
            .AsNoTracking()
            .Where(set => ids.Contains(set.OwnerId) && set.Slot == slot && set.Conditions.Count > 0)
            .Select(set => set.OwnerId)
            .ToListAsync(ct);

        return [.. ids.Except(withSet)];
    }
}
