using GameDevManager.Data.Assets;
using GameDevManager.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddDbContextFactory<GameDevManagerDbContext>(builder =>
            builder.UseGameDevManagerProvider(options.Provider, connectionString));

        return services.AddGameDevManagerContentServices();
    }

    /// <summary>
    /// Registriert die fachlichen Dienste. Sie legen sich ihren DbContext je Aufruf über die
    /// Factory an und sind deshalb ohne Zustand — <c>Scoped</c> genügt.
    /// </summary>
    public static IServiceCollection AddGameDevManagerContentServices(this IServiceCollection services)
    {
        services.AddScoped<ContentTypeService>();
        services.AddScoped<ItemService>();
        services.AddScoped<CraftingService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<NpcService>();
        services.AddScoped<LootService>();
        services.AddScoped<MapService>();
        services.AddScoped<ConditionService>();
        services.AddScoped<DialogueService>();
        services.AddScoped<ReferenceService>();
        services.AddScoped<AssetService>();
        services.AddScoped<SearchService>();

        // Je Modul eine Quelle. Referenzansicht, Auswahlfelder, Arten-Zählung und globale
        // Suche fragen sie alle ab — ein neues Modul wird hier eingetragen und ist überall da.
        services.AddSingleton<IModuleEntitySource, ItemEntitySource>();
        services.AddSingleton<IModuleEntitySource, RecipeEntitySource>();
        services.AddSingleton<IModuleEntitySource, CurrencyEntitySource>();
        services.AddSingleton<IModuleEntitySource, NpcEntitySource>();
        services.AddSingleton<IModuleEntitySource, LootTableEntitySource>();
        services.AddSingleton<IModuleEntitySource, MapEntitySource>();
        services.AddSingleton<IModuleEntitySource, DialogueEntitySource>();

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
