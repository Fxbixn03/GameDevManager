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
