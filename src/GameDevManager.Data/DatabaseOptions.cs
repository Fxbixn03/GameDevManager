namespace GameDevManager.Data;

/// <summary>
/// Wird aus dem Konfigurationsabschnitt "Database" gebunden.
/// Der Connection-String wird aus "ConnectionStrings:{Provider}" gelesen.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Beim App-Start automatisch ausstehende Migrationen anwenden.</summary>
    public bool AutoMigrate { get; set; } = true;
}
