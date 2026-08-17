namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Rolle bündelt die vier Rechte, die sonst je Konto einzeln gepflegt werden — „Autor“,
/// „Grafiker“, „Nur lesen“. Ein Konto verweist auf höchstens eine Rolle; die Auflösung bleibt
/// an der einen Stelle (<c>UserPermissions.For</c>), die Rolle ist dort die Vorgabe und das
/// Konto darf abweichen.
/// <para>
/// Bewusst dieselben Spalten wie am <see cref="AppUser"/> und keine Vererbung: Rollen hängen
/// wie Benutzer an keinem Projekt, und das Verwalterrecht bleibt eine Sache des Kontos — eine
/// Rolle „Verwalter“ wäre ein zweiter Weg zur höchsten Stufe.
/// </para>
/// </summary>
public class UserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Installationsweit eindeutig — zwei Rollen „Autor“ wären nicht zu unterscheiden.</summary>
    public required string Name { get; set; }

    public bool CanWrite { get; set; } = true;

    public bool CanExport { get; set; } = true;

    public bool CanImport { get; set; } = true;

    /// <summary>Kommagetrennte Modul-Schlüssel, <c>null</c> heißt alle — wie am Konto.</summary>
    public string? AllowedModuleKeys { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
