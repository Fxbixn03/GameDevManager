using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Das Spieler-Modul: Spielerfiguren, Skilltrees und Skills samt benutzerdefinierten
/// Feldwerten der Skills.
/// </summary>
public class PlayerService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    // ------------------------------------------------------------- Spielerfiguren

    public async Task<List<PlayerCharacter>> GetCharactersAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.PlayerCharacters
            .AsNoTracking()
            .Where(p => p.GameProjectId == projectId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task SaveCharacterAsync(PlayerCharacter character, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(character.Name))
        {
            throw new ContentValidationException(messages["CharacterNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.PlayerCharacters.FirstOrDefaultAsync(p => p.Id == character.Id, ct);

        if (stored is null)
        {
            stored = new PlayerCharacter
            {
                Id = character.Id,
                GameProjectId = character.GameProjectId,
                Name = character.Name.Trim(),
                CreatedAtUtc = now
            };

            db.PlayerCharacters.Add(stored);
        }

        stored.Name = character.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(character.Description) ? null : character.Description.Trim();
        stored.CharacterClassId = character.CharacterClassId;
        stored.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Überführt alle Spielerfiguren des Projekts in NPCs — der Umbau aus der ToDo-Liste:
    /// „Der Spieler wird zukünftig als NPC behandelt.“ Die GUID bleibt dieselbe, damit
    /// Sprites, Feldwerte, Bedingungen, Tags und Karten-Markierungen weiter auf die Figur
    /// zeigen; nur ihr Modul-Schlüssel wird umgeschrieben. Der neue NPC ist einzigartig —
    /// eine Spielerfigur gibt es genau einmal.
    /// </summary>
    public async Task<int> ConvertCharactersToNpcsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var characters = await db.PlayerCharacters
            .AsNoTracking()
            .Where(p => p.GameProjectId == projectId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        if (characters.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        foreach (var character in characters)
        {
            db.Npcs.Add(new Npc
            {
                Id = character.Id,
                GameProjectId = projectId,
                Name = character.Name,
                Description = character.Description,
                CharacterClassId = character.CharacterClassId,
                Kind = NpcKind.Npc,
                IsUnique = true,
                CreatedAtUtc = character.CreatedAtUtc,
                UpdatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(ct);

        var ids = characters.Select(c => c.Id).ToList();

        // Alles, was über die GUID an der Figur hängt, wandert per Modul-Schlüssel mit.
        await db.Assets
            .Where(a => a.OwnerEntityId != null && ids.Contains(a.OwnerEntityId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.OwnerModuleKey, ModuleKeys.Npcs), ct);

        await db.FieldValues
            .Where(v => ids.Contains(v.OwnerEntityId))
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.OwnerModuleKey, ModuleKeys.Npcs), ct);

        await db.FieldDefinitions
            .Where(f => f.OwnerEntityId != null && ids.Contains(f.OwnerEntityId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.ModuleKey, ModuleKeys.Npcs), ct);

        await db.ConditionSets
            .Where(c => ids.Contains(c.OwnerId))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerModuleKey, ModuleKeys.Npcs), ct);

        await db.Conditions
            .Where(c => c.TargetEntityId != null && ids.Contains(c.TargetEntityId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TargetModuleKey, ModuleKeys.Npcs), ct);

        await db.ContentTagAssignments
            .Where(a => ids.Contains(a.TargetEntityId))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.TargetModuleKey, ModuleKeys.Npcs), ct);

        await db.MapMarkers
            .Where(m => m.TargetEntityId != null && ids.Contains(m.TargetEntityId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.TargetModuleKey, ModuleKeys.Npcs), ct);

        await db.StoryParticipants
            .Where(p => ids.Contains(p.TargetEntityId))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TargetModuleKey, ModuleKeys.Npcs), ct);

        await db.PlayerCharacters
            .Where(p => p.GameProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);

        return characters.Count;
    }

    public async Task DeleteCharacterAsync(Guid characterId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(characterId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.PlayerCharacters, characterId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, characterId, ct);

        await db.PlayerCharacters
            .Where(p => p.Id == characterId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // ----------------------------------------------------------------- Skilltrees

    public async Task<List<SkillTreeRow>> GetTreesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.SkillTrees
            .AsNoTracking()
            .Where(t => t.GameProjectId == projectId)
            .OrderBy(t => t.Name)
            .Select(t => new SkillTreeRow(
                t.Id,
                t.Name,
                t.Description,
                db.Skills.Count(s => s.SkillTreeId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task SaveTreeAsync(SkillTree tree, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tree.Name))
        {
            throw new ContentValidationException(messages["SkillTreeNameRequired"]);
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var stored = await db.SkillTrees.FirstOrDefaultAsync(t => t.Id == tree.Id, ct);

        if (stored is null)
        {
            stored = new SkillTree
            {
                Id = tree.Id,
                GameProjectId = tree.GameProjectId,
                Name = tree.Name.Trim()
            };

            db.SkillTrees.Add(stored);
        }

        stored.Name = tree.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(tree.Description) ? null : tree.Description.Trim();

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht einen Skilltree. Seine Skills bleiben erhalten und werden „ohne Baum“.</summary>
    public async Task DeleteTreeAsync(Guid treeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.SkillTrees, treeId, ct);

        // Kein Fremdschlüssel über die GUID — die Verweise müssen von Hand gelöst werden,
        // sonst zeigten Skills auf einen Baum, den es nicht mehr gibt.
        await db.Skills
            .Where(s => s.SkillTreeId == treeId)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.SkillTreeId, (Guid?)null), ct);

        await db.SkillTrees
            .Where(t => t.Id == treeId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // --------------------------------------------------------------------- Skills

    public async Task<List<SkillListRow>> GetSkillsAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Skills
            .AsNoTracking()
            .Where(s => s.GameProjectId == projectId)
            .OrderBy(s => s.Name)
            .Select(s => new SkillListRow(
                s.Id,
                s.Name,
                s.Description,
                s.SkillTreeId,
                db.SkillTrees.Where(t => t.Id == s.SkillTreeId).Select(t => t.Name).FirstOrDefault(),
                s.ParentSkillId,
                db.Skills.Where(p => p.Id == s.ParentSkillId).Select(p => p.Name).FirstOrDefault(),
                s.CostPoints,
                s.CostItemId,
                db.Items.Where(i => i.Id == s.CostItemId).Select(i => i.Name).FirstOrDefault(),
                s.CostItemAmount,
                s.ContentTypeId,
                s.ContentType!.Name,
                s.UpdatedAtUtc,
                db.Assets
                    .Where(a => a.OwnerEntityId == s.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<ContentEditContext<Skill>?> LoadSkillForEditAsync(
        Guid projectId, Guid? skillId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Player, ct);

        if (skillId is null)
        {
            return new ContentEditContext<Skill>
            {
                Entity = new Skill { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var skill = await db.Skills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == skillId && s.GameProjectId == projectId, ct);

        if (skill is null)
        {
            return null;
        }

        return new ContentEditContext<Skill>
        {
            Entity = skill,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, skill.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, skill.Id, ct)
        };
    }

    public async Task SaveSkillAsync(ContentEditContext<Skill> context, CancellationToken ct = default)
    {
        var skill = context.Entity;

        if (string.IsNullOrWhiteSpace(skill.Name))
        {
            throw new ContentValidationException(messages["SkillNameRequired"]);
        }

        if (skill.CostPoints is < 0)
        {
            throw new ContentValidationException(messages["SkillPointsNegative"]);
        }

        if (skill.CostItemAmount is < 1 && skill.CostItemId is not null)
        {
            throw new ContentValidationException(messages["SkillCostAmountMin"]);
        }

        if (skill.CostItemId is null)
        {
            // Eine Menge ohne Item wäre nicht zu deuten.
            skill.CostItemAmount = null;
        }

        if (skill.ParentSkillId == skill.Id)
        {
            throw new ContentValidationException(messages["SkillSelfParent"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        if (skill.ParentSkillId is { } parentId)
        {
            var parent = await db.Skills
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == parentId && p.GameProjectId == skill.GameProjectId, ct);

            if (parent is null)
            {
                throw new ContentValidationException(messages["SkillParentGone"]);
            }

            if (parent.SkillTreeId != skill.SkillTreeId)
            {
                throw new ContentValidationException(messages["SkillParentOtherTree"]);
            }

            // Die Elternkette darf nicht zurück zu diesem Skill führen — sonst wäre der
            // Baum ein Kreis und kein Skill davon je erreichbar.
            var byId = await db.Skills
                .AsNoTracking()
                .Where(s => s.GameProjectId == skill.GameProjectId)
                .Select(s => new { s.Id, s.ParentSkillId })
                .ToDictionaryAsync(s => s.Id, s => s.ParentSkillId, ct);

            var cursor = (Guid?)parentId;
            while (cursor is { } currentId && byId.TryGetValue(currentId, out var next))
            {
                if (currentId == skill.Id)
                {
                    throw new ContentValidationException(messages["SkillParentCycle"]);
                }

                cursor = next;
            }
        }

        var now = DateTime.UtcNow;
        var stored = await db.Skills.FirstOrDefaultAsync(s => s.Id == skill.Id, ct);

        if (stored is null)
        {
            stored = new Skill
            {
                Id = skill.Id,
                GameProjectId = skill.GameProjectId,
                Name = skill.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Skills.Add(stored);
        }

        stored.ContentTypeId = skill.ContentTypeId;
        stored.Name = skill.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(skill.Description) ? null : skill.Description.Trim();
        stored.SkillTreeId = skill.SkillTreeId;
        stored.ParentSkillId = skill.ParentSkillId;
        stored.CostPoints = skill.CostPoints;
        stored.CostItemId = skill.CostItemId;
        stored.CostItemAmount = skill.CostItemAmount;
        stored.UpdatedAtUtc = now;

        await ContentFields.StageValuesAsync(db, context, messages, ct);
        await db.SaveChangesAsync(ct);

        skill.CreatedAtUtc = stored.CreatedAtUtc;
        skill.UpdatedAtUtc = stored.UpdatedAtUtc;
        skill.Name = stored.Name;
        skill.Description = stored.Description;
    }

    /// <summary>Löscht einen Skill mit Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteSkillAsync(Guid skillId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(skillId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await ChangeLog.RecordDeletionAsync(db, db.Skills, skillId, ct);
        await EntityCleanup.DeleteForEntityAsync(db, skillId, ct);

        // Kinder verlieren ihre Voraussetzung, statt auf einen gelöschten Skill zu zeigen.
        await db.Skills
            .Where(s => s.ParentSkillId == skillId)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.ParentSkillId, (Guid?)null), ct);

        await db.Skills
            .Where(s => s.Id == skillId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
