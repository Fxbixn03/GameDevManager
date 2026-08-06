using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data;

public class GameDevManagerDbContext(DbContextOptions<GameDevManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<GameProject> GameProjects => Set<GameProject>();

    /// <summary>Benutzerdefinierte Arten aller Module (Item-Art „Waffe", NPC-Art „Händler", …).</summary>
    public DbSet<ContentType> ContentTypes => Set<ContentType>();

    /// <summary>Felddefinitionen — entweder an einer Art oder an einer einzelnen Entität.</summary>
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();

    public DbSet<FieldOption> FieldOptions => Set<FieldOption>();

    /// <summary>Feldwerte aller Module, adressiert über die GUID der besitzenden Entität.</summary>
    public DbSet<FieldValue> FieldValues => Set<FieldValue>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Npc> Npcs => Set<Npc>();

    public DbSet<TraderOffer> TraderOffers => Set<TraderOffer>();

    public DbSet<LootTable> LootTables => Set<LootTable>();

    public DbSet<LootEntry> LootEntries => Set<LootEntry>();

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

        ConfigureContentEntity<Npc>(modelBuilder);

        modelBuilder.Entity<Npc>(entity =>
        {
            // Die Übersicht filtert fast immer nach NPC/Mob und nach Rolle.
            entity.HasIndex(n => new { n.GameProjectId, n.Kind });
            entity.HasIndex(n => n.IsTrader);

            // Trägt die Frage „welche NPCs benutzen diese Loot-Table?“.
            entity.HasIndex(n => n.LootTableId);
        });

        ConfigureContentEntity<LootTable>(modelBuilder);

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

        modelBuilder.Entity<Recipe>(entity =>
        {
            // Zeigt auf ein Item, also über die Modulgrenze — deshalb nur die GUID.
            entity.HasIndex(r => r.OutputItemId);
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
