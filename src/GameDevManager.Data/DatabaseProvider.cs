namespace GameDevManager.Data;

/// <summary>
/// Unterstützte Datenbank-Provider, in der priorisierten Reihenfolge des Projekts.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql,
    Sqlite
}
