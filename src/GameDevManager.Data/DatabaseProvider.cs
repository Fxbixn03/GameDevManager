namespace GameDevManager.Data;

/// <summary>
/// Unterstützte Datenbank-Provider, in der priorisierten Reihenfolge des Projekts.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql,
    Sqlite,

    /// <summary>
    /// Oracle Database über den offiziellen Provider (<c>Oracle.EntityFrameworkCore</c>).
    /// Referenz-Zielversion für Self-Hoster ist die kostenlose Database Free (23ai);
    /// <c>bool</c> liegt dort als NUMBER(1), <c>Guid</c> als RAW(16) — die GUID-Referenzen
    /// des Konzepts funktionieren damit unverändert.
    /// </summary>
    Oracle
}
