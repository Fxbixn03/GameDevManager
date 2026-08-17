using System.Text.Json;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Welcher Entitätstyp hinter welcher Inhaltsdatei steht — die Zuordnung, die der Teil-Import
/// braucht, um eine einzelne Entität aus dem Archiv zu lesen und zu schreiben.
/// <para>
/// Eine Aufzählung und keine Reflection: Es sind zwanzig Zeilen, sie ändern sich genau dann,
/// wenn ein Modul dazukommt, und der Compiler prüft jede davon. Eine Zuordnung über Namen wäre
/// bei der ersten Umbenennung still falsch.
/// </para>
/// </summary>
internal static class ImportTypes
{
    /// <summary>Liest eine Entität aus ihrem JSON — <c>null</c>, wenn die Datei unbekannt ist.</summary>
    internal static ContentEntity? Read(string file, string json) => file switch
    {
        "items.json" => Parse<Item>(json),
        "crafting.json" => Parse<Recipe>(json),
        "currencies.json" => Parse<Currency>(json),
        "rarities.json" => Parse<Rarity>(json),
        "npcs.json" => Parse<Npc>(json),
        "factions.json" => Parse<Faction>(json),
        "diplomacy.json" => Parse<DiplomaticRelation>(json),
        "maps.json" => Parse<GameMap>(json),
        "dialogs.json" => Parse<Dialogue>(json),
        "story.json" => Parse<StoryEntry>(json),
        "quests.json" => Parse<Quest>(json),
        "events.json" => Parse<GameEvent>(json),
        "classes.json" => Parse<CharacterClass>(json),
        "loot.json" => Parse<LootTable>(json),
        "world.json" => Parse<WorldState>(json),
        "effects.json" => Parse<GameEffect>(json),
        "achievements.json" => Parse<Achievement>(json),
        "collectibles.json" => Parse<Collectible>(json),
        "audio.json" => Parse<SoundEffect>(json),
        "cutscenes.json" => Parse<Cutscene>(json),
        _ => null
    };

    /// <summary>
    /// Entfernt eine vorhandene Entität samt ihrer Kind-Sammlungen — die fallen über die
    /// Fremdschlüssel mit. Ohne das ließe sich ein bestehender Stand nicht überschreiben.
    /// </summary>
    internal static async Task DeleteAsync(
        GameDevManagerDbContext db, string file, Guid entityId, CancellationToken ct)
    {
        Func<Task>? delete = file switch
        {
            "items.json" => () => Remove(db.Items, entityId, ct),
            "crafting.json" => () => Remove(db.Recipes, entityId, ct),
            "currencies.json" => () => Remove(db.Currencies, entityId, ct),
            "rarities.json" => () => Remove(db.Rarities, entityId, ct),
            "npcs.json" => () => Remove(db.Npcs, entityId, ct),
            "factions.json" => () => Remove(db.Factions, entityId, ct),
            "diplomacy.json" => () => Remove(db.DiplomaticRelations, entityId, ct),
            "maps.json" => () => Remove(db.Maps, entityId, ct),
            "dialogs.json" => () => Remove(db.Dialogues, entityId, ct),
            "story.json" => () => Remove(db.StoryEntries, entityId, ct),
            "quests.json" => () => Remove(db.Quests, entityId, ct),
            "events.json" => () => Remove(db.GameEvents, entityId, ct),
            "classes.json" => () => Remove(db.CharacterClasses, entityId, ct),
            "loot.json" => () => Remove(db.LootTables, entityId, ct),
            "world.json" => () => Remove(db.WorldStates, entityId, ct),
            "effects.json" => () => Remove(db.GameEffects, entityId, ct),
            "achievements.json" => () => Remove(db.Achievements, entityId, ct),
            "collectibles.json" => () => Remove(db.Collectibles, entityId, ct),
            "audio.json" => () => Remove(db.SoundEffects, entityId, ct),
            "cutscenes.json" => () => Remove(db.Cutscenes, entityId, ct),
            _ => null
        };

        if (delete is not null)
        {
            await delete();
        }
    }

    private static TEntity? Parse<TEntity>(string json) where TEntity : ContentEntity =>
        JsonSerializer.Deserialize<TEntity>(json, ExportFormat.JsonOptions);

    private static Task Remove<TEntity>(DbSet<TEntity> set, Guid entityId, CancellationToken ct)
        where TEntity : ContentEntity =>
        set.Where(entity => entity.Id == entityId).ExecuteDeleteAsync(ct);
}
