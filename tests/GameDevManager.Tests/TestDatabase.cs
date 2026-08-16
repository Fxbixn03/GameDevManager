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

        // Wie in der Anwendung: scoped und mit beiden Interceptoren, damit Schreibschutz und
        // Änderungsprotokoll in den Tests genauso greifen wie im Betrieb.
        services.AddDbContextFactory<GameDevManagerDbContext>(
            (provider, builder) => builder
                .UseSqlite(_connection)
                .AddInterceptors(
                    provider.GetRequiredService<WriteGuardInterceptor>(),
                    provider.GetRequiredService<ChangeLogInterceptor>()),
            ServiceLifetime.Scoped);

        services.AddSingleton<IAssetStorage, InMemoryAssetStorage>();
        services.AddSingleton(new AssetStorageOptions { RootPath = Path.GetTempPath() });
        ExportOptions = new ExportStorageOptions { RootPath = _exportPath };
        services.AddSingleton(ExportOptions);
        services.AddScoped<ExportSnapshotService>();

        // Vor den Inhaltsdiensten registriert, deren TryAdd damit die Vorgabe stehen lässt —
        // so kann ein Test die Aufbewahrung des Protokolls umstellen.
        services.AddSingleton(ChangeLogRetention);
        services.AddGameDevManagerContentServices();

        // Ersetzt die Vorgabe „System“ — so lässt sich prüfen, wer im Protokoll landet.
        services.AddScoped<IChangeAuthorProvider>(_ => Author);

        // Ebenso die Berechtigungen: Vorgabe „alles erlaubt“, umstellbar je Test — im
        // Betrieb kommen sie aus den Ansprüchen des Anmelde-Cookies.
        services.AddScoped<IUserPermissionsProvider>(_ => Permissions);

        // Ebenso die Passwortrichtlinie: veränderbar, damit ein Test sie umstellen kann —
        // im Betrieb kommt sie aus der Einstellungsseite der Benutzerverwaltung.
        services.AddSingleton<IPasswordPolicyProvider>(_ => Policy);

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

    /// <summary>Die Passwortrichtlinie der Tests — Vorgabe, bis ein Test sie umstellt.</summary>
    public MutablePasswordPolicyProvider Policy { get; } = new();

    /// <summary>Die Berechtigungen des handelnden Benutzers — „alles erlaubt“, bis ein Test sie umstellt.</summary>
    public MutableUserPermissionsProvider Permissions { get; } = new();

    /// <summary>
    /// Aus dem Scope und nicht aus dem Wurzel-Container: Die Context-Factory ist scoped
    /// registriert, weil der Interceptor den handelnden Benutzer braucht.
    /// </summary>
    public GameDevManagerDbContext CreateContext() =>
        _scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>().CreateDbContext();

    public T GetService<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Wo die Exportstände dieses Tests liegen — der Ordner vergeht mit ihm.</summary>
    public string ExportPath => _exportPath;

    /// <summary>
    /// Der Dateispeicher der Exportstände samt Aufbewahrung. Veränderbar, damit ein Test die
    /// Grenzen umstellen kann — im Betrieb kommen sie aus dem Abschnitt „Exports“.
    /// </summary>
    public ExportStorageOptions ExportOptions { get; }

    /// <summary>
    /// Wie weit das Änderungsprotokoll zurückreicht. Ebenfalls veränderbar — im Betrieb kommt
    /// es aus dem Abschnitt „ChangeLog“, im Test steht die Vorgabe „unbegrenzt“, damit ein
    /// Wartungslauf keinem anderen Test die Einträge unter den Füßen wegzieht.
    /// </summary>
    public ChangeLogRetentionOptions ChangeLogRetention { get; } =
        new() { MaxAgeDays = 0, MaxPerProject = 0 };

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

    /// <summary>Eine Richtlinie, die der Test umstellen kann — im Betrieb kommt sie aus der Konfiguration.</summary>
    public sealed class MutablePasswordPolicyProvider : IPasswordPolicyProvider
    {
        public PasswordPolicy Current { get; set; } = PasswordPolicy.Default;
    }

    /// <summary>Berechtigungen, die der Test umstellen kann — im Betrieb kommen sie aus dem Cookie.</summary>
    public sealed class MutableUserPermissionsProvider : IUserPermissionsProvider
    {
        public UserPermissions Current { get; set; } = UserPermissions.Full;

        public ValueTask<UserPermissions> GetCurrentAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(Current);
    }

    /// <summary>Ein Urheber, den der Test umstellen kann — im Betrieb kommt er aus der Anmeldung.</summary>
    public sealed class MutableChangeAuthorProvider : IChangeAuthorProvider
    {
        public ChangeAuthor Current { get; set; } = new(Guid.NewGuid(), "Testbenutzer");

        public ValueTask<ChangeAuthor> GetCurrentAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(Current);
    }

    /// <summary>
    /// Dateispeicher-Attrappe: es geht in den Tests nie um echte Dateien. Die Schlüssel führt
    /// sie trotzdem mit — die Suche nach verwaisten Dateien braucht die Gegenrichtung.
    /// </summary>
    public sealed class InMemoryAssetStorage : IAssetStorage
    {
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

        public Task<string> SaveAsync(
            Guid projectId, Guid assetId, string extension, Stream content, CancellationToken ct = default)
        {
            var key = $"{projectId:N}/{assetId:N}{extension}";
            _keys.Add(key);

            return Task.FromResult(key);
        }

        public Stream? OpenRead(string storageKey) => null;

        public void Delete(string storageKey) => _keys.Remove(storageKey);

        public IReadOnlyList<string> ListKeys() => [.. _keys.Order(StringComparer.Ordinal)];

        /// <summary>Legt einen Schlüssel ab, ohne dass es dazu eine Zeile gäbe — ein Waise.</summary>
        public void AddStrayFile(string storageKey) => _keys.Add(storageKey);
    }
}
