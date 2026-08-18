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
    Oracle,

    /// <summary>
    /// MariaDB über die Pomelo-Codebasis — vorerst als Microting-Fork
    /// (<c>Microting.EntityFrameworkCore.MySql</c>), weil das offizielle Pomelo nur bis
    /// EF Core 9 vorliegt; der Umzug auf das Original ist Issue #52. Bewusst getrennt vom
    /// <see cref="MySql"/>-Provider: Oracles <c>MySql.EntityFrameworkCore</c> unterstützt
    /// MariaDB offiziell nicht, und Pomelo erzeugt für MariaDB teils anderes SQL — deshalb
    /// auch ein eigenes Migrations-Projekt statt geteilter Migrationen.
    /// </summary>
    MariaDb
}
