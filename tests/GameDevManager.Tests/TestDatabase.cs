using GameDevManager.Data;
using GameDevManager.Data.Assets;
using GameDevManager.Data.Services;
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
    private readonly IServiceScope _scope;
    private readonly string _exportPath;

    public TestDatabase()
    {
        // Die In-Memory-Datenbank lebt, solange diese Verbindung offen ist.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Exportstände liegen im Dateisystem — der ersetzende Import und das Löschen eines
        // Projekts legen davor einen an. Je Test ein eigenes Verzeichnis, das mit ihm vergeht.
        _exportPath = Path.Combine(Path.GetTempPath(), $"gdm-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_exportPath);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();

        // Wie in der Anwendung: scoped und mit dem ChangeLogInterceptor, damit das
        // Änderungsprotokoll in den Tests genauso mitschreibt wie im Betrieb.
        services.AddDbContextFactory<GameDevManagerDbContext>(
            (provider, builder) => builder
                .UseSqlite(_connection)
                .AddInterceptors(provider.GetRequiredService<ChangeLogInterceptor>()),
            ServiceLifetime.Scoped);

        services.AddSingleton<IAssetStorage, InMemoryAssetStorage>();
        services.AddSingleton(new AssetStorageOptions { RootPath = Path.GetTempPath() });
        services.AddSingleton(new ExportStorageOptions { RootPath = _exportPath });
        services.AddScoped<ExportSnapshotService>();
        services.AddGameDevManagerContentServices();

        // Ersetzt die Vorgabe „System“ — so lässt sich prüfen, wer im Protokoll landet.
        services.AddScoped<IChangeAuthorProvider>(_ => Author);

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        using var db = CreateContext();
        db.Database.EnsureCreated();

        var project = new GameProject { Name = "Testprojekt" };
        db.GameProjects.Add(project);
        db.SaveChanges();

        ProjectId = project.Id;
    }

    public Guid ProjectId { get; }

    /// <summary>
    /// Wer im Änderungsprotokoll als Urheber steht. Veränderbar, damit ein Test zwei Benutzer
    /// nacheinander arbeiten lassen kann.
    /// </summary>
    public MutableChangeAuthorProvider Author { get; } = new();

    /// <summary>
    /// Aus dem Scope und nicht aus dem Wurzel-Container: Die Context-Factory ist scoped
    /// registriert, weil der Interceptor den handelnden Benutzer braucht.
    /// </summary>
    public GameDevManagerDbContext CreateContext() =>
        _scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>().CreateDbContext();

    public T GetService<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Wo die Exportstände dieses Tests liegen — der Ordner vergeht mit ihm.</summary>
    public string ExportPath => _exportPath;

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();

        if (Directory.Exists(_exportPath))
        {
            Directory.Delete(_exportPath, recursive: true);
        }
    }

    /// <summary>Ein Urheber, den der Test umstellen kann — im Betrieb kommt er aus der Anmeldung.</summary>
    public sealed class MutableChangeAuthorProvider : IChangeAuthorProvider
    {
        public ChangeAuthor Current { get; set; } = new(Guid.NewGuid(), "Testbenutzer");

        public ValueTask<ChangeAuthor> GetCurrentAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(Current);
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
