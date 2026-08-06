using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data.Services;

/// <summary>
/// Der Zugang zu den Entitäten eines Moduls für alle modulübergreifenden Dienste:
/// Referenzansicht, Referenz-Auswahlfelder, Arten-Verwendungszählung und globale Suche.
/// <para>
/// Ohne diese Schnittstelle müsste jedes neue Modul in vier verstreute <c>switch</c>-Blöcke
/// eingetragen werden — vergisst man einen, fehlt das Modul stillschweigend in einer Ansicht.
/// So ist es eine Klasse je Modul, die entweder da ist oder nicht.
/// </para>
/// <para>
/// Die Methoden bekommen den DbContext übergeben, statt sich einen zu holen: die
/// aufrufenden Dienste fragen mehrere Module nacheinander ab und sollen das in einem
/// Kontext tun können.
/// </para>
/// </summary>
public interface IModuleEntitySource
{
    /// <summary>Das Modul, das diese Quelle bedient — siehe <see cref="Domain.ModuleKeys"/>.</summary>
    string ModuleKey { get; }

    /// <summary>Wie viele Entitäten eine Art verwenden. Trägt den Löschschutz für Arten.</summary>
    Task<int> CountByTypeAsync(GameDevManagerDbContext db, Guid typeId, CancellationToken ct);

    /// <summary>Die auswählbaren Entitäten des Moduls, alphabetisch.</summary>
    Task<List<EntitySummary>> GetEntitiesAsync(GameDevManagerDbContext db, Guid projectId, CancellationToken ct);

    /// <summary>Löst GUIDs auf ihre Anzeigenamen auf.</summary>
    Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, List<Guid> ids, CancellationToken ct);

    /// <summary>Volltextsuche über Name und Beschreibung. <paramref name="needle"/> ist kleingeschrieben.</summary>
    Task<List<SearchHit>> SearchAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct);

    /// <summary>Löst eine GUID auf, falls sie zu diesem Modul gehört.</summary>
    Task<SearchHit?> FindByIdAsync(GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct);

    /// <summary>
    /// Stellen, an denen dieses Modul über eigene Spalten auf eine fremde Entität verweist —
    /// also nicht über Feldwerte. Rezept-Zutaten sind der erste Fall; Händler-Angebote und
    /// Loot-Einträge kommen so dazu. Module ohne solche Verweise geben nichts zurück.
    /// </summary>
    Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct);
}

/// <summary>
/// Gemeinsame Umsetzung für alle Module, deren Entitäten von <see cref="ContentEntity"/>
/// abgeleitet sind. Zählen, Auflisten und Namen auflösen läuft überall gleich; nur die
/// Suchtreffer bekommen je Modul eine eigene Beschriftung.
/// </summary>
public abstract class ModuleEntitySource<TEntity> : IModuleEntitySource
    where TEntity : ContentEntity
{
    public abstract string ModuleKey { get; }

    protected abstract DbSet<TEntity> Set(GameDevManagerDbContext db);

    public Task<int> CountByTypeAsync(GameDevManagerDbContext db, Guid typeId, CancellationToken ct) =>
        Set(db).CountAsync(entity => entity.ContentTypeId == typeId, ct);

    public Task<List<EntitySummary>> GetEntitiesAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct)
    {
        // In eine lokale Variable ziehen: eine Eigenschaft der Klasse könnte EF nicht übersetzen.
        var moduleKey = ModuleKey;

        return Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId)
            .OrderBy(entity => entity.Name)
            .Select(entity => new EntitySummary(entity.Id, moduleKey, entity.Name, entity.ContentType!.Name))
            .ToListAsync(ct);
    }

    public Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, List<Guid> ids, CancellationToken ct) =>
        Set(db)
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Name, ct);

    public Task<List<SearchHit>> SearchAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct) =>
        Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId
                && (entity.Name.ToLower().Contains(needle)
                    || (entity.Description != null && entity.Description.ToLower().Contains(needle))))
            .OrderBy(entity => entity.Name)
            .Take(limit))
        .ToListAsync(ct);

    public async Task<SearchHit?> FindByIdAsync(
        GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct) =>
        await Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.Id == id && entity.GameProjectId == projectId))
        .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Standardfall: Das Modul verweist nur über benutzerdefinierte Referenzfelder auf andere
    /// Entitäten, und die wertet der <see cref="ReferenceService"/> selbst aus. Module mit
    /// eigenen Verknüpfungsspalten überschreiben das.
    /// <para>
    /// Bewusst virtuell in der Basisklasse und nicht als Standardimplementierung an der
    /// Schnittstelle: die Zuordnung zur Schnittstelle entsteht hier, eine gleichnamige Methode
    /// in einer abgeleiteten Klasse würde sie nicht ersetzen und stillschweigend nie laufen.
    /// </para>
    /// </summary>
    public virtual Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct) =>
        Task.FromResult(new List<EntityReferenceHit>());

    /// <summary>
    /// Bildet Entitäten auf Suchtreffer ab. Je Modul eigen, weil Untertitel und Vorschaubild
    /// unterschiedlich zustande kommen — beim Item die Art, beim Rezept das Ergebnis.
    /// </summary>
    protected abstract IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<TEntity> query);
}
