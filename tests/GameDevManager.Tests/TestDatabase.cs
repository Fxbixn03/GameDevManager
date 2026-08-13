using GameDevManager.Data;
using GameDevManager.Data.Assets;
using GameDevManager.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameDevManager.Tests;

/// <summary>
/// Eine echte Datenbank je Test: SQLite im Speicher, Schema aus dem EF-Modell. Die Dienste
/// kommen aus demselben DI-Aufbau wie in der Anwendung (<c>AddGameDevManagerContentServices</c>
/// samt aller <c>IModuleEntitySource</c>) — ersetzt sind nur Datenbankanbindung und
/// Dateispeicher. Ein Standardprojekt ist bereits angelegt (<see cref="ProjectId"/>).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public TestDatabase()
    {
        // Die In-Memory-Datenbank lebt, solange diese Verbindung offen ist.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddDbContextFactory<GameDevManagerDbContext>(builder => builder.UseSqlite(_connection));
        services.AddSingleton<IAssetStorage, InMemoryAssetStorage>();
        services.AddSingleton(new AssetStorageOptions { RootPath = Path.GetTempPath() });
        services.AddGameDevManagerContentServices();

        _provider = services.BuildServiceProvider();

        using var db = CreateContext();
        db.Database.EnsureCreated();

        var project = new GameProject { Name = "Testprojekt" };
        db.GameProjects.Add(project);
        db.SaveChanges();

        ProjectId = project.Id;
    }

    public Guid ProjectId { get; }

    public GameDevManagerDbContext CreateContext() =>
        _provider.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>().CreateDbContext();

    public T GetService<T>() where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>Dateispeicher-Attrappe: es geht in den Tests nie um echte Dateien.</summary>
    private sealed class InMemoryAssetStorage : IAssetStorage
    {
        public Task<string> SaveAsync(
            Guid projectId, Guid assetId, string extension, Stream content, CancellationToken ct = default) =>
            Task.FromResult($"{projectId:N}/{assetId:N}{extension}");

        public Stream? OpenRead(string storageKey) => null;

        public void Delete(string storageKey)
        {
        }
    }
}
