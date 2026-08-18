using GameDevManager.Data.Assets;
using GameDevManager.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameDevManager.Data;

public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Registriert den DbContext (als Factory, Blazor-Server-tauglich) mit dem in der
    /// Konfiguration gewählten Provider. Erwartete Konfiguration:
    /// <code>
    /// "Database": { "Provider": "Sqlite", "AutoMigrate": true },
    /// "ConnectionStrings": { "Sqlite": "Data Source=gamedevmanager.db", ... }
    /// </code>
    /// </summary>
    public static IServiceCollection AddGameDevManagerDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                      ?? new DatabaseOptions();

        var connectionString = configuration.GetConnectionString(options.Provider.ToString())
            ?? throw new InvalidOperationException(
                $"Kein Connection-String für Provider '{options.Provider}' gefunden. " +
                $"Bitte 'ConnectionStrings:{options.Provider}' in der Konfiguration setzen.");

        services.AddSingleton(options);

        // Wie weit das Änderungsprotokoll zurückreicht. Steht hier und nicht in
        // AddGameDevManagerContentServices, weil nur dieser Weg die Konfiguration kennt —
        // dort wird die Vorgabe nur nachgetragen, falls niemand sie gesetzt hat.
        services.AddSingleton(
            configuration.GetSection(ChangeLogRetentionOptions.SectionName).Get<ChangeLogRetentionOptions>()
            ?? new ChangeLogRetentionOptions());

        // Wie lange der Papierkorb zurückreicht — dieselbe Linie, dieselbe Stelle.
        services.AddSingleton(
            configuration.GetSection(RecycleBinOptions.SectionName).Get<RecycleBinOptions>()
            ?? new RecycleBinOptions());

        // Die Factory ist bewusst scoped und nicht — wie sonst üblich — Singleton: Der
        // ChangeLogInterceptor muss wissen, wer gerade angemeldet ist, und das steht je
        // Verbindung fest. Contexts entstehen weiterhin je Aufruf; nur die Factory selbst
        // lebt jetzt so lange wie die Verbindung. Wer sie beim Start aus dem Wurzel-Container
        // holt, braucht dafür einen Scope (siehe Program.cs).
        services.AddDbContextFactory<GameDevManagerDbContext>(
            (provider, builder) => builder
                .UseGameDevManagerProvider(options.Provider, connectionString)
                // Der Schreibschutz zuerst: Wer nicht schreiben darf, soll abgewiesen werden,
                // bevor das Protokoll Einträge anlegt.
                .AddInterceptors(
                    provider.GetRequiredService<WriteGuardInterceptor>(),
                    provider.GetRequiredService<ChangeLogInterceptor>()),
            ServiceLifetime.Scoped);

        return services.AddGameDevManagerContentServices();
    }

    /// <summary>
    /// Registriert die fachlichen Dienste. Sie legen sich ihren DbContext je Aufruf über die
    /// Factory an und sind deshalb ohne Zustand — <c>Scoped</c> genügt.
    /// </summary>
    public static IServiceCollection AddGameDevManagerContentServices(this IServiceCollection services)
    {
        // Das Änderungsprotokoll schreibt sich beim Speichern selbst mit — es hängt am
        // DbContext und nicht an den Modul-Diensten. Wer gerade handelt, beantwortet die
        // Web-Schicht; ohne Anmeldung bleibt es bei „System“.
        services.AddScoped<ChangeLogInterceptor>();
        services.TryAddScoped<IChangeAuthorProvider>(_ => new SystemChangeAuthorProvider());

        // Der Schreibschutz nach demselben Muster: zentral am SaveChanges, die Antwort auf
        // „was darf der Benutzer?“ kommt im Betrieb aus der Web-Schicht. Ohne Ersatz gilt
        // „alles erlaubt“ — der Zustand ohne Anmeldung (Start, Ersteinrichtung, Tests).
        services.AddScoped<WriteGuardInterceptor>();
        services.TryAddScoped<IUserPermissionsProvider>(_ => new FullUserPermissionsProvider());
        services.AddScoped<PermissionGuard>();
        // Ohne Konfiguration die Vorgabe — der Zustand in den Tests; im Betrieb steht sie
        // bereits aus dem Abschnitt „ChangeLog“ registriert da.
        services.TryAddSingleton(new ChangeLogRetentionOptions());
        services.TryAddSingleton(new RecycleBinOptions());
        services.AddScoped<ChangeLogService>();
        services.AddScoped<RecycleBinService>();
        services.AddScoped<UserService>();
        // Die Passwortrichtlinie kommt im Betrieb aus der Web-Schicht (Konfiguration plus
        // Einstellungsseite); ohne Ersatz gilt die Vorgabe — dasselbe Muster wie der Urheber.
        services.TryAddSingleton<IPasswordPolicyProvider, DefaultPasswordPolicyProvider>();

        services.AddScoped<ProjectService>();
        services.AddScoped<ContentTypeService>();
        services.AddScoped<ModuleSettingsService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<DashboardOverviewService>();
        services.AddScoped<ItemService>();
        services.AddScoped<CraftingService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<RarityService>();
        services.AddScoped<NpcService>();
        services.AddScoped<FactionService>();
        services.AddScoped<DiplomacyService>();
        services.AddScoped<StoryService>();
        services.AddScoped<QuestService>();
        services.AddScoped<EventService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<ClassService>();
        services.AddScoped<EffectService>();
        services.AddScoped<AchievementService>();
        services.AddScoped<CollectibleService>();
        services.AddScoped<TagService>();
        services.AddScoped<AudioService>();
        services.AddScoped<CutsceneService>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<TechTreeService>();
        services.AddScoped<ConnectionsService>();
        services.AddScoped<KanbanService>();
        services.AddScoped<WhiteboardService>();
        services.AddSingleton<WhiteboardNotifier>();

        // Wer welche Maske offen hat — reiner Arbeitsspeicher, siehe EditingPresence.
        services.AddSingleton<EditingPresence>();

        // Dauer und Ergebnis der Hintergrundläufe — reiner Arbeitsspeicher, für /api/v1/metrics.
        services.AddSingleton<BackgroundRunTracker>();
        services.AddScoped<WorldService>();
        services.AddScoped<LootService>();
        services.AddScoped<UserPinService>();
        services.AddScoped<OperationsMetricsService>();
        services.AddScoped<HealthCheckMuteService>();
        services.AddScoped<ExportProfileService>();
        services.AddScoped<ContentCommentService>();
        services.AddScoped<ActivityFeedService>();
        services.AddScoped<EconomyService>();
        services.AddScoped<ProgressionService>();
        services.AddScoped<MentionResolver>();
        services.AddScoped<MapService>();
        services.AddScoped<ConditionService>();
        services.AddScoped<DialogueService>();
        services.AddScoped<VoiceOverService>();
        services.AddScoped<ReferenceService>();
        services.AddScoped<AssetService>();
        services.AddScoped<SearchService>();
        services.AddScoped<CurveService>();
        services.AddScoped<BulkEditService>();
        services.AddScoped<SeriesService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<SavedViewService>();
        services.AddScoped<ContentRuleService>();
        services.AddScoped<WebhookService>();

        // Die Warteschlange ist ein Singleton: Der Interceptor stellt je Verbindung ein, der
        // Zustelldienst leert prozessweit — dieselbe Bauart wie EditingPresence.
        services.TryAddSingleton<WebhookQueue>();
        services.AddScoped<CsvContentService>();
        services.AddScoped<LocalizationService>();
        services.AddScoped<EnginePresetService>();
        services.AddScoped<EngineExportWriter>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<ContentApiService>();
        services.AddScoped<ContentApiWriteService>();
        services.AddScoped<EntityDuplicationService>();
        services.AddScoped<StartScreenService>();
        services.AddScoped<ExportService>();
        services.AddScoped<ImportService>();
        services.AddScoped<InstallationBackupService>();
        services.AddScoped<PartialImportService>();
        services.AddScoped<SampleProjectService>();
        services.AddScoped<ConditionStateService>();
        services.AddScoped<DesignDocumentService>();

        // Je Modul eine Quelle. Referenzansicht, Auswahlfelder, Arten-Zählung und globale
        // Suche fragen sie alle ab — ein neues Modul wird hier eingetragen und ist überall da.
        services.AddSingleton<IModuleEntitySource, ItemEntitySource>();
        services.AddSingleton<IModuleEntitySource, RecipeEntitySource>();
        services.AddSingleton<IModuleEntitySource, CurrencyEntitySource>();
        services.AddSingleton<IModuleEntitySource, RarityEntitySource>();
        services.AddSingleton<IModuleEntitySource, NpcEntitySource>();
        services.AddSingleton<IModuleEntitySource, FactionEntitySource>();
        services.AddSingleton<IModuleEntitySource, DiplomaticRelationEntitySource>();
        services.AddSingleton<IModuleEntitySource, StoryEntrySource>();
        services.AddSingleton<IModuleEntitySource, QuestEntitySource>();
        services.AddSingleton<IModuleEntitySource, GameEventEntitySource>();
        services.AddSingleton<IModuleEntitySource, SkillEntitySource>();
        services.AddSingleton<IModuleEntitySource, CharacterClassEntitySource>();
        services.AddSingleton<IModuleEntitySource, GameEffectEntitySource>();
        services.AddSingleton<IModuleEntitySource, AchievementEntitySource>();
        services.AddSingleton<IModuleEntitySource, CollectibleEntitySource>();
        services.AddSingleton<IModuleEntitySource, SoundEffectEntitySource>();
        services.AddSingleton<IModuleEntitySource, CutsceneEntitySource>();
        services.AddSingleton<IModuleEntitySource, LootTableEntitySource>();
        services.AddSingleton<IModuleEntitySource, MapEntitySource>();
        services.AddSingleton<IModuleEntitySource, DialogueEntitySource>();
        services.AddSingleton<IModuleEntitySource, WorldStateEntitySource>();

        return services;
    }

    /// <summary>
    /// Richtet den Dateispeicher für Assets ein. <paramref name="basePath"/> ist die Wurzel,
    /// gegen die ein relativ konfigurierter Pfad aufgelöst wird — üblicherweise das
    /// Anwendungsverzeichnis.
    /// </summary>
    public static IServiceCollection AddGameDevManagerAssetStorage(
        this IServiceCollection services, IConfiguration configuration, string basePath)
    {
        var options = configuration.GetSection(AssetStorageOptions.SectionName).Get<AssetStorageOptions>()
                      ?? new AssetStorageOptions();

        options.RootPath = Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(basePath, options.StoragePath);

        Directory.CreateDirectory(options.RootPath);

        services.AddSingleton(options);
        services.AddSingleton<IAssetStorage, FileSystemAssetStorage>();

        return services;
    }

    /// <summary>
    /// Richtet den Dateispeicher für aufbewahrte Exportstände ein — dasselbe Muster wie beim
    /// Asset-Speicher: relativ konfigurierte Pfade werden gegen <paramref name="basePath"/>
    /// aufgelöst, üblicherweise das Anwendungsverzeichnis.
    /// </summary>
    public static IServiceCollection AddGameDevManagerExportStorage(
        this IServiceCollection services, IConfiguration configuration, string basePath)
    {
        var options = configuration.GetSection(ExportStorageOptions.SectionName).Get<ExportStorageOptions>()
                      ?? new ExportStorageOptions();

        options.RootPath = Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(basePath, options.StoragePath);

        Directory.CreateDirectory(options.RootPath);

        services.AddSingleton(options);
        services.AddScoped<ExportSnapshotService>();

        return services;
    }

    /// <summary>
    /// Opens a connection with the given provider and connection string once, then closes it.
    /// Throws with the provider's error message when the database is unreachable — deliberately
    /// not <c>CanConnectAsync</c>, which swallows the cause the settings dialog wants to show.
    /// </summary>
    public static async Task TestConnectionAsync(
        DatabaseProvider provider, string connectionString, CancellationToken ct = default)
    {
        var builder = new DbContextOptionsBuilder<GameDevManagerDbContext>();
        builder.UseGameDevManagerProvider(provider, connectionString);

        await using var db = new GameDevManagerDbContext(builder.Options);
        await db.Database.OpenConnectionAsync(ct);
        await db.Database.CloseConnectionAsync();
    }

    /// <summary>
    /// Wählt den EF-Core-Provider und das zugehörige Migrations-Assembly.
    /// Jeder Provider hat sein eigenes Migrations-Projekt, da Migrationen nicht
    /// zwischen Providern portabel sind.
    /// </summary>
    public static DbContextOptionsBuilder UseGameDevManagerProvider(
        this DbContextOptionsBuilder builder, DatabaseProvider provider, string connectionString)
        => provider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString,
                x => x.MigrationsAssembly("GameDevManager.Data.Migrations.SqlServer")),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString,
                x => x.MigrationsAssembly("GameDevManager.Data.Migrations.PostgreSql")),
            DatabaseProvider.MySql => builder.UseMySQL(connectionString,
                x => x.MigrationsAssembly("GameDevManager.Data.Migrations.MySql")),
            DatabaseProvider.Sqlite => builder.UseSqlite(connectionString,
                x => x.MigrationsAssembly("GameDevManager.Data.Migrations.Sqlite")),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter Datenbank-Provider.")
        };
}
