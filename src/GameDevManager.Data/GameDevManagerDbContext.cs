using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data;

public class GameDevManagerDbContext(DbContextOptions<GameDevManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<GameProject> GameProjects => Set<GameProject>();

    /// <summary>Module on/off per project — no row means "enabled".</summary>
    public DbSet<ModuleSetting> ModuleSettings => Set<ModuleSetting>();

    /// <summary>Sichtbarkeit und Reihenfolge der Dashboard-Cards, je Projekt.</summary>
    public DbSet<DashboardCard> DashboardCards => Set<DashboardCard>();

    /// <summary>Benutzerdefinierte Arten aller Module (Item-Art „Waffe", NPC-Art „Händler", …).</summary>
    public DbSet<ContentType> ContentTypes => Set<ContentType>();

    /// <summary>Felddefinitionen — entweder an einer Art oder an einer einzelnen Entität.</summary>
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();

    public DbSet<FieldOption> FieldOptions => Set<FieldOption>();

    /// <summary>Feldwerte aller Module, adressiert über die GUID der besitzenden Entität.</summary>
    public DbSet<FieldValue> FieldValues => Set<FieldValue>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeOutput> RecipeOutputs => Set<RecipeOutput>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Rarity> Rarities => Set<Rarity>();

    public DbSet<Npc> Npcs => Set<Npc>();

    public DbSet<TraderOffer> TraderOffers => Set<TraderOffer>();

    public DbSet<Faction> Factions => Set<Faction>();

    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();

    public DbSet<DiplomaticRelation> DiplomaticRelations => Set<DiplomaticRelation>();

    public DbSet<StoryEntry> StoryEntries => Set<StoryEntry>();

    public DbSet<StoryParticipant> StoryParticipants => Set<StoryParticipant>();

    public DbSet<Quest> Quests => Set<Quest>();

    public DbSet<GameEvent> GameEvents => Set<GameEvent>();

    public DbSet<EventSpawn> EventSpawns => Set<EventSpawn>();

    public DbSet<PlayerCharacter> PlayerCharacters => Set<PlayerCharacter>();

    public DbSet<SkillTree> SkillTrees => Set<SkillTree>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<CharacterClass> CharacterClasses => Set<CharacterClass>();

    public DbSet<GameEffect> GameEffects => Set<GameEffect>();

    public DbSet<EffectAssignment> EffectAssignments => Set<EffectAssignment>();

    public DbSet<Achievement> Achievements => Set<Achievement>();

    public DbSet<Collectible> Collectibles => Set<Collectible>();

    /// <summary>Modulübergreifende Tags; die Asset-Stichwörter bleiben davon getrennt.</summary>
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();

    public DbSet<ContentTagScope> ContentTagScopes => Set<ContentTagScope>();

    public DbSet<ContentTagAssignment> ContentTagAssignments => Set<ContentTagAssignment>();

    public DbSet<SoundEffect> SoundEffects => Set<SoundEffect>();

    public DbSet<Cutscene> Cutscenes => Set<Cutscene>();

    public DbSet<CutsceneShot> CutsceneShots => Set<CutsceneShot>();

    public DbSet<LootTable> LootTables => Set<LootTable>();

    public DbSet<LootEntry> LootEntries => Set<LootEntry>();

    /// <summary>Bedingungssätze aller Module, adressiert über die GUID ihres Besitzers.</summary>
    public DbSet<ConditionSet> ConditionSets => Set<ConditionSet>();

    public DbSet<Condition> Conditions => Set<Condition>();

    public DbSet<Dialogue> Dialogues => Set<Dialogue>();

    public DbSet<DialogueParticipant> DialogueParticipants => Set<DialogueParticipant>();

    public DbSet<DialogueLine> DialogueLines => Set<DialogueLine>();

    public DbSet<DialogueChoice> DialogueChoices => Set<DialogueChoice>();

    public DbSet<GameMap> Maps => Set<GameMap>();

    public DbSet<MapMarker> MapMarkers => Set<MapMarker>();

    /// <summary>Hochgeladene Dateien aller Module; die Datei selbst liegt im Dateispeicher.</summary>
    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AssetTag> AssetTags => Set<AssetTag>();

    public DbSet<AssetTagAssignment> AssetTagAssignments => Set<AssetTagAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GameProject>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(4000);
        });

        modelBuilder.Entity<ModuleSetting>(entity =>
        {
            entity.Property(s => s.ModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();

            entity.HasOne(s => s.GameProject)
                .WithMany()
                .HasForeignKey(s => s.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.GameProjectId, s.ModuleKey }).IsUnique();
        });

        modelBuilder.Entity<DashboardCard>(entity =>
        {
            entity.Property(c => c.CardKey).HasMaxLength(ModuleKeyLength).IsRequired();

            entity.HasOne(c => c.GameProject)
                .WithMany()
                .HasForeignKey(c => c.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.GameProjectId, c.CardKey }).IsUnique();
        });

        modelBuilder.Entity<ContentType>(entity =>
        {
            entity.Property(t => t.ModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(4000);
            entity.Property(t => t.Icon).HasMaxLength(100);

            entity.HasOne(t => t.GameProject)
                .WithMany()
                .HasForeignKey(t => t.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Arten werden immer je Projekt und Modul geladen.
            entity.HasIndex(t => new { t.GameProjectId, t.ModuleKey });
        });

        modelBuilder.Entity<FieldDefinition>(entity =>
        {
            entity.Property(f => f.ModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();
            entity.Property(f => f.Name).HasMaxLength(200).IsRequired();
            entity.Property(f => f.Description).HasMaxLength(2000);
            entity.Property(f => f.Unit).HasMaxLength(30);
            entity.Property(f => f.ReferenceModuleKey).HasMaxLength(ModuleKeyLength);
            entity.Ignore(f => f.IsIndividual);

            // Art-Felder verschwinden mit ihrer Art; individuelle Felder hängen an keiner
            // Fremdschlüsselbeziehung und werden vom ContentService mit der Entität entfernt.
            entity.HasOne(f => f.ContentType)
                .WithMany(t => t.Fields)
                .HasForeignKey(f => f.ContentTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => f.ContentTypeId);
            entity.HasIndex(f => f.OwnerEntityId);
        });

        modelBuilder.Entity<FieldOption>(entity =>
        {
            entity.Property(o => o.Label).HasMaxLength(200).IsRequired();

            entity.HasOne(o => o.FieldDefinition)
                .WithMany(f => f.Options)
                .HasForeignKey(o => o.FieldDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldValue>(entity =>
        {
            entity.Property(v => v.OwnerModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();
            entity.Ignore(v => v.IsEmpty);

            entity.HasOne(v => v.FieldDefinition)
                .WithMany()
                .HasForeignKey(v => v.FieldDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Pro Entität und Feld gibt es höchstens einen Wert.
            entity.HasIndex(v => new { v.OwnerEntityId, v.FieldDefinitionId }).IsUnique();

            // Trägt die Referenzansicht („Find All References"): wer zeigt auf diese GUID?
            entity.HasIndex(v => v.ReferenceValue);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.Property(a => a.OwnerModuleKey).HasMaxLength(ModuleKeyLength);
            entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.MimeType).HasMaxLength(100).IsRequired();
            entity.Property(a => a.StorageKey).HasMaxLength(400).IsRequired();
            entity.Property(a => a.Description).HasMaxLength(2000);
            entity.Ignore(a => a.IsToolAsset);

            entity.HasOne(a => a.GameProject)
                .WithMany()
                .HasForeignKey(a => a.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.GameProjectId);

            // Trägt sowohl die Sprite-Liste einer Entität als auch die Suche nach dem primären Sprite.
            entity.HasIndex(a => new { a.OwnerEntityId, a.IsPrimary });
        });

        modelBuilder.Entity<AssetTag>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Color).HasMaxLength(20);

            entity.HasOne(t => t.GameProject)
                .WithMany()
                .HasForeignKey(t => t.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ein Stichwort gibt es je Projekt nur einmal; der AssetService prüft das vorher
            // und meldet es verständlich, dieser Index ist die Absicherung dahinter.
            entity.HasIndex(t => new { t.GameProjectId, t.Name }).IsUnique();
        });

        modelBuilder.Entity<AssetTagAssignment>(entity =>
        {
            entity.HasKey(a => new { a.AssetId, a.AssetTagId });

            entity.HasOne(a => a.Asset)
                .WithMany(asset => asset.Tags)
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Tag)
                .WithMany(tag => tag.Assignments)
                .HasForeignKey(a => a.AssetTagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureContentEntity<Item>(modelBuilder);
        ConfigureContentEntity<Recipe>(modelBuilder);
        ConfigureContentEntity<Currency>(modelBuilder);

        modelBuilder.Entity<Currency>(entity => entity.Property(c => c.Symbol).HasMaxLength(10));

        ConfigureContentEntity<Rarity>(modelBuilder);

        // „#RRGGBB“ oder „#RRGGBBAA“ — mehr braucht eine Anzeigefarbe nicht.
        modelBuilder.Entity<Rarity>(entity => entity.Property(r => r.Color).HasMaxLength(9));

        ConfigureContentEntity<Npc>(modelBuilder);

        modelBuilder.Entity<Npc>(entity =>
        {
            // Die Übersicht filtert fast immer nach NPC/Mob und nach Rolle.
            entity.HasIndex(n => new { n.GameProjectId, n.Kind });
            entity.HasIndex(n => n.IsTrader);

            // Trägt die Frage „welche NPCs benutzen diese Loot-Table?“.
            entity.HasIndex(n => n.LootTableId);

            // Trägt die Frage „welche NPCs haben diese Klasse?“.
            entity.HasIndex(n => n.CharacterClassId);
        });

        modelBuilder.Entity<ConditionSet>(entity =>
        {
            entity.Property(s => s.OwnerModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();
            entity.Property(s => s.Slot).HasMaxLength(50).IsRequired();

            entity.HasOne(s => s.GameProject)
                .WithMany()
                .HasForeignKey(s => s.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Je Besitzer und Aspekt gibt es höchstens einen Satz.
            entity.HasIndex(s => new { s.OwnerId, s.Slot }).IsUnique();
        });

        modelBuilder.Entity<Condition>(entity =>
        {
            entity.Property(c => c.TargetModuleKey).HasMaxLength(ModuleKeyLength);
            entity.Property(c => c.TextValue).HasMaxLength(500);
            entity.Ignore(c => c.UsesNumber);
            entity.Ignore(c => c.UsesBoolean);
            entity.Ignore(c => c.UsesTarget);
            entity.Ignore(c => c.ExpectedTargetModule);

            entity.HasOne(c => c.ConditionSet)
                .WithMany(s => s.Conditions)
                .HasForeignKey(c => c.ConditionSetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „welche Bedingungen beziehen sich auf diese Entität?“.
            entity.HasIndex(c => c.TargetEntityId);
        });

        ConfigureContentEntity<Dialogue>(modelBuilder);

        modelBuilder.Entity<DialogueParticipant>(entity =>
        {
            entity.HasOne(p => p.Dialogue)
                .WithMany(d => d.Participants)
                .HasForeignKey(p => p.DialogueId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „an welchen Dialogen ist dieser NPC beteiligt?“.
            entity.HasIndex(p => p.NpcId);
        });

        modelBuilder.Entity<DialogueLine>(entity =>
        {
            entity.Property(l => l.Text).HasMaxLength(4000).IsRequired();

            entity.HasOne(l => l.Dialogue)
                .WithMany(d => d.Lines)
                .HasForeignKey(l => l.DialogueId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => l.SpeakerNpcId);
        });

        modelBuilder.Entity<DialogueChoice>(entity =>
        {
            entity.Property(c => c.Text).HasMaxLength(1000).IsRequired();

            entity.HasOne(c => c.Line)
                .WithMany(l => l.Choices)
                .HasForeignKey(c => c.DialogueLineId)
                .OnDelete(DeleteBehavior.Cascade);

            // NextLineId zeigt auf eine Zeile desselben Dialogs. Bewusst ohne Fremdschlüssel:
            // ein solcher liefe im Kreis zurück auf dieselbe Tabelle, und die Löschregeln
            // wären über die Provider hinweg nicht einheitlich zu bekommen.
            entity.HasIndex(c => c.NextLineId);
        });

        ConfigureContentEntity<Faction>(modelBuilder);

        modelBuilder.Entity<FactionMember>(entity =>
        {
            entity.Property(m => m.Role).HasMaxLength(200);

            entity.HasOne(m => m.Faction)
                .WithMany(f => f.Members)
                .HasForeignKey(m => m.FactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „in welchen Fraktionen ist dieser NPC Mitglied?“.
            entity.HasIndex(m => m.NpcId);
        });

        ConfigureContentEntity<DiplomaticRelation>(modelBuilder);

        modelBuilder.Entity<DiplomaticRelation>(entity =>
        {
            // Trägt die Frage „in welchen Beziehungen steckt diese Fraktion?“ — für beide Seiten.
            entity.HasIndex(r => r.FactionAId);
            entity.HasIndex(r => r.FactionBId);
        });

        ConfigureContentEntity<StoryEntry>(modelBuilder);

        modelBuilder.Entity<StoryEntry>(entity =>
        {
            // Der Zeitstreifen lädt immer projektweise in dieser Reihenfolge.
            entity.HasIndex(s => new { s.GameProjectId, s.SortOrder });
        });

        modelBuilder.Entity<StoryParticipant>(entity =>
        {
            entity.Property(p => p.TargetModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();

            entity.HasOne(p => p.StoryEntry)
                .WithMany(s => s.Participants)
                .HasForeignKey(p => p.StoryEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „an welchen Story-Abschnitten ist diese Entität beteiligt?“.
            entity.HasIndex(p => p.TargetEntityId);
        });

        ConfigureContentEntity<Quest>(modelBuilder);

        modelBuilder.Entity<Quest>(entity =>
        {
            // Die Übersicht filtert nach Haupt-/Nebenmission/Event.
            entity.HasIndex(q => new { q.GameProjectId, q.Kind });

            // Tragen die Referenzansicht der verknüpften Entitäten.
            entity.HasIndex(q => q.GiverNpcId);
            entity.HasIndex(q => q.StoryEntryId);
            entity.HasIndex(q => q.DialogueId);
        });

        ConfigureContentEntity<GameEvent>(modelBuilder);

        modelBuilder.Entity<GameEvent>(entity =>
        {
            // Trägt die Frage „welche Events belohnen mit diesem Loot-Table?“.
            entity.HasIndex(e => e.RewardLootTableId);
        });

        modelBuilder.Entity<EventSpawn>(entity =>
        {
            entity.HasOne(s => s.GameEvent)
                .WithMany(e => e.Spawns)
                .HasForeignKey(s => s.GameEventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „bei welchen Events spawnt dieser Mob?“.
            entity.HasIndex(s => s.NpcId);
        });

        modelBuilder.Entity<PlayerCharacter>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(4000);

            entity.HasOne(p => p.GameProject)
                .WithMany()
                .HasForeignKey(p => p.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.GameProjectId);

            // Trägt die Frage „welche Spielerfiguren haben diese Klasse?“.
            entity.HasIndex(p => p.CharacterClassId);
        });

        modelBuilder.Entity<SkillTree>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(4000);

            entity.HasOne(t => t.GameProject)
                .WithMany()
                .HasForeignKey(t => t.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => t.GameProjectId);
        });

        ConfigureContentEntity<Skill>(modelBuilder);

        modelBuilder.Entity<Skill>(entity =>
        {
            // Die Baum-Ansicht lädt Skills je Baum; die Struktur hängt am Eltern-Skill.
            entity.HasIndex(s => s.SkillTreeId);
            entity.HasIndex(s => s.ParentSkillId);

            // Trägt die Frage „welche Skills kosten dieses Item?“.
            entity.HasIndex(s => s.CostItemId);
        });

        ConfigureContentEntity<CharacterClass>(modelBuilder);

        ConfigureContentEntity<GameEffect>(modelBuilder);

        modelBuilder.Entity<EffectAssignment>(entity =>
        {
            entity.HasOne(a => a.GameEffect)
                .WithMany(e => e.Assignments)
                .HasForeignKey(a => a.GameEffectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „welche Effekte hat dieses Item?“.
            entity.HasIndex(a => a.ItemId);
        });

        ConfigureContentEntity<Achievement>(modelBuilder);

        ConfigureContentEntity<Collectible>(modelBuilder);

        ConfigureContentEntity<SoundEffect>(modelBuilder);

        ConfigureContentEntity<Cutscene>(modelBuilder);

        modelBuilder.Entity<Cutscene>(entity =>
        {
            // Tragen die Referenzansicht von Story-Abschnitten und Dialogen.
            entity.HasIndex(c => c.StoryEntryId);
            entity.HasIndex(c => c.DialogueId);
        });

        modelBuilder.Entity<CutsceneShot>(entity =>
        {
            entity.Property(s => s.Text).HasMaxLength(2000).IsRequired();

            entity.HasOne(s => s.Cutscene)
                .WithMany(c => c.Shots)
                .HasForeignKey(s => s.CutsceneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentTag>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.Color).HasMaxLength(20);

            entity.HasOne(t => t.GameProject)
                .WithMany()
                .HasForeignKey(t => t.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ein Tag gibt es je Projekt nur einmal; der TagService prüft das vorher und
            // meldet es verständlich, dieser Index ist die Absicherung dahinter.
            entity.HasIndex(t => new { t.GameProjectId, t.Name }).IsUnique();
        });

        modelBuilder.Entity<ContentTagScope>(entity =>
        {
            entity.Property(s => s.ModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();

            entity.HasOne(s => s.ContentTag)
                .WithMany(t => t.Scopes)
                .HasForeignKey(s => s.ContentTagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentTagAssignment>(entity =>
        {
            entity.Property(a => a.TargetModuleKey).HasMaxLength(ModuleKeyLength).IsRequired();

            entity.HasOne(a => a.ContentTag)
                .WithMany(t => t.Assignments)
                .HasForeignKey(a => a.ContentTagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „welche Tags hat diese Entität?“; je Entität und Tag höchstens einmal.
            entity.HasIndex(a => new { a.TargetEntityId, a.ContentTagId }).IsUnique();
        });

        ConfigureContentEntity<LootTable>(modelBuilder);
        ConfigureContentEntity<GameMap>(modelBuilder);

        modelBuilder.Entity<MapMarker>(entity =>
        {
            entity.Property(m => m.Label).HasMaxLength(200);
            entity.Property(m => m.TargetModuleKey).HasMaxLength(ModuleKeyLength);
            entity.Property(m => m.Color).HasMaxLength(20);
            entity.Ignore(m => m.IsArea);
            entity.Ignore(m => m.IsMapLink);

            entity.HasOne(m => m.Map)
                .WithMany(map => map.Markers)
                .HasForeignKey(m => m.MapId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „wo auf den Karten kommt diese Entität vor?“.
            entity.HasIndex(m => m.TargetEntityId);
        });

        modelBuilder.Entity<LootEntry>(entity =>
        {
            entity.HasOne(e => e.LootTable)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.LootTableId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „in welchen Loot-Tables kommt dieses Item vor?“.
            entity.HasIndex(e => e.ItemId);
        });

        modelBuilder.Entity<TraderOffer>(entity =>
        {
            entity.HasOne(o => o.Npc)
                .WithMany(n => n.Offers)
                .HasForeignKey(o => o.NpcId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Fragen „wer handelt mit diesem Item?“ und „wo wird diese Währung benutzt?“.
            entity.HasIndex(o => o.ItemId);
            entity.HasIndex(o => o.CurrencyId);
        });

        modelBuilder.Entity<RecipeOutput>(entity =>
        {
            entity.HasOne(o => o.Recipe)
                .WithMany(r => r.Outputs)
                .HasForeignKey(o => o.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „welche Rezepte stellen dieses Item her?“.
            entity.HasIndex(o => o.ItemId);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasOne(i => i.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Trägt die Frage „welche Rezepte brauchen dieses Item?“.
            entity.HasIndex(i => i.ItemId);
        });
    }

    /// <summary>
    /// Gemeinsame Abbildung aller Modul-Entitäten. Jedes Modul bekommt eine eigene Tabelle,
    /// teilt sich aber Aufbau und Beziehungen der Basis.
    /// </summary>
    private static void ConfigureContentEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : ContentEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);

            // Nur eine Konstante der konkreten Klasse, keine Spalte.
            entity.Ignore(e => e.ModuleKey);

            entity.HasOne(e => e.GameProject)
                .WithMany()
                .HasForeignKey(e => e.GameProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bewusst kein Cascade: eine Art, die noch verwendet wird, darf nicht
            // stillschweigend Inhalte mitreißen — der ContentService blockt das vorher ab.
            entity.HasOne(e => e.ContentType)
                .WithMany()
                .HasForeignKey(e => e.ContentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.GameProjectId);
            entity.HasIndex(e => e.ContentTypeId);
            entity.HasIndex(e => e.Name);
        });
    }

    /// <summary>Modul-Schlüssel sind kurze Bezeichner; die Länge hält die Indizes MySQL-tauglich.</summary>
    private const int ModuleKeyLength = 50;
}
