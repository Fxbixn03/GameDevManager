using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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

    /// <summary>
    /// Ob sich Entitäten dieses Moduls kopieren lassen. Fast überall ja; ausgenommen sind
    /// Module, in denen ein zweiter Datensatz derselben Sache keine Vorlage, sondern ein
    /// Widerspruch wäre — siehe <see cref="DiplomaticRelationEntitySource"/>.
    /// </summary>
    bool CanDuplicate { get; }

    /// <summary>Wie viele Entitäten eine Art verwenden. Trägt den Löschschutz für Arten.</summary>
    Task<int> CountByTypeAsync(GameDevManagerDbContext db, Guid typeId, CancellationToken ct);

    /// <summary>Die auswählbaren Entitäten des Moduls, alphabetisch.</summary>
    Task<List<EntitySummary>> GetEntitiesAsync(GameDevManagerDbContext db, Guid projectId, CancellationToken ct);

    /// <summary>Löst GUIDs auf ihre Anzeigenamen auf.</summary>
    Task<Dictionary<Guid, string>> ResolveNamesAsync(
        GameDevManagerDbContext db, List<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Lädt Entitäten des Projekts <b>verfolgt</b> für die Massenbearbeitung. Verfolgt und
    /// nicht über <c>ExecuteUpdate</c>, damit Schreibschutz und Änderungsprotokoll greifen —
    /// beide hängen am <c>SaveChanges</c> und sähen ein Massen-Update sonst nie.
    /// </summary>
    Task<List<ContentEntity>> LoadForBulkAsync(
        GameDevManagerDbContext db, Guid projectId, IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Der ganze Bestand des Moduls im Projekt, verfolgt — für den CSV-Export und den
    /// CSV-Import, der bestehende Zeilen über GUID oder Name wiederfindet.
    /// </summary>
    Task<List<ContentEntity>> LoadAllAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct);

    /// <summary>
    /// Legt eine neue, noch nicht angehängte Entität dieses Moduls an — der CSV-Import erzeugt
    /// damit Zeilen, die es noch nicht gibt.
    /// </summary>
    ContentEntity CreateNew(Guid projectId, string name);

    /// <summary>
    /// Volltextsuche über Name und Beschreibung — Module mit eigenen Texten (Dialogzeilen)
    /// suchen zusätzlich dort. <paramref name="needle"/> ist kleingeschrieben.
    /// </summary>
    Task<List<SearchHit>> SearchAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct);

    /// <summary>
    /// Suche über die Textwerte der benutzerdefinierten Felder dieses Moduls. Getrennt von
    /// <see cref="SearchAsync"/>, weil die Oberfläche solche Treffer anders beschriftet: Der
    /// gesuchte Text steht nicht im Namen, sondern in einem Feld der Entität.
    /// </summary>
    Task<List<SearchHit>> SearchFieldValuesAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct);

    /// <summary>Löst eine GUID auf, falls sie zu diesem Modul gehört.</summary>
    Task<SearchHit?> FindByIdAsync(GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct);

    /// <summary>
    /// Eine Stichprobe der Entitäten des Moduls — bisher für die Dekoration des Startscreens.
    /// Sortiert wird nach GUID und nicht nach Namen: die Reihenfolge ist damit beliebig statt
    /// alphabetisch, sonst käme immer nur der Anfang des Alphabets heraus. Welcher Ausschnitt
    /// gezogen wird, entscheidet der Zufall — jede Entität des Moduls muss drankommen können.
    /// </summary>
    Task<List<SearchHit>> SampleAsync(
        GameDevManagerDbContext db, Guid projectId, int limit, CancellationToken ct);

    /// <summary>
    /// Die zuletzt bearbeiteten Entitäten des Moduls, jüngste zuerst — das „Weiterarbeiten“
    /// des Dashboards. Wie bei <see cref="SampleAsync"/> genügt eine Methode je Modul, damit
    /// ein neues Modul von selbst mitkommt.
    /// </summary>
    Task<List<RecentEntry>> RecentAsync(
        GameDevManagerDbContext db, Guid projectId, int limit, CancellationToken ct);

    /// <summary>
    /// Kopiert eine Entität samt Kind-Sammlungen und allem, was an ihrer GUID hängt, und hängt
    /// sie an den Kontext an — gespeichert wird vom Aufrufer. <c>null</c>, wenn es die Entität
    /// nicht (mehr) gibt.
    /// </summary>
    Task<Guid?> DuplicateAsync(
        GameDevManagerDbContext db, Guid entityId, string name, CancellationToken ct);

    /// <summary>
    /// Schreibt eine Entität samt allem, was an ihren GUIDs hängt, in einen JSON-Text — die
    /// Vorlage für den Papierkorb. <c>null</c>, wenn es die Entität nicht (mehr) gibt.
    /// <para>
    /// Dieselbe Strecke wie <see cref="DuplicateAsync"/>, nur ohne den GUID-Tausch: Zurück soll
    /// genau dieser Datensatz kommen und nicht ein zweiter.
    /// </para>
    /// </summary>
    Task<RecycledEntity?> CaptureAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct);

    /// <summary>
    /// Liest einen aufbewahrten Baum zurück und hängt ihn an den Kontext an — gespeichert wird
    /// vom Aufrufer. Die GUIDs bleiben die originalen, damit jeder Verweis wieder trägt.
    /// </summary>
    void Restore(GameDevManagerDbContext db, string payload);

    /// <summary>
    /// Stellen, an denen dieses Modul über eigene Spalten auf eine fremde Entität verweist —
    /// also nicht über Feldwerte. Rezept-Zutaten sind der erste Fall; Händler-Angebote und
    /// Loot-Einträge kommen so dazu. Module ohne solche Verweise geben nichts zurück.
    /// </summary>
    Task<List<EntityReferenceHit>> FindReferencesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct);

    /// <summary>
    /// Texte des Moduls, die <b>nicht</b> in Name, Beschreibung oder einem benutzerdefinierten
    /// Feld stehen — Dialogzeilen, Antwortmöglichkeiten, Cutscene-Einstellungen, der Story-Text.
    /// <para>
    /// Ohne diesen Weg erfasste die Lokalisierung ausgerechnet die textlastigsten Inhalte des
    /// Tools nicht. Dasselbe Muster wie bei <see cref="FindReferencesAsync"/>: Wer nichts
    /// zusätzlich hat, meldet nichts, und ein künftiges Modul ist von selbst dabei.
    /// </para>
    /// </summary>
    Task<List<TranslatableText>> GetTranslatableTextsAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct);
}

/// <summary>
/// Gemeinsame Umsetzung für alle Module, deren Entitäten von <see cref="ContentEntity"/>
/// abgeleitet sind. Zählen, Auflisten und Namen auflösen läuft überall gleich; nur die
/// Suchtreffer bekommen je Modul eine eigene Beschriftung.
/// </summary>
public abstract class ModuleEntitySource<TEntity>(IStringLocalizer<DataMessages> messages)
    : IModuleEntitySource
    where TEntity : ContentEntity
{
    /// <summary>
    /// Untertitel in Suche und Referenzansicht sind Text für die Oberfläche und stehen deshalb
    /// in <c>DataMessages.resx</c>. Vor einer LINQ-Abfrage in eine lokale Variable ziehen —
    /// einen Indexer-Aufruf kann EF nicht übersetzen.
    /// </summary>
    protected IStringLocalizer<DataMessages> Messages { get; } = messages;

    public abstract string ModuleKey { get; }

    /// <summary>
    /// Standardfall: Kopieren ist erlaubt. Virtuell aus demselben Grund wie
    /// <see cref="FindReferencesAsync"/>.
    /// </summary>
    public virtual bool CanDuplicate => true;

    protected abstract DbSet<TEntity> Set(GameDevManagerDbContext db);

    public Task<int> CountByTypeAsync(GameDevManagerDbContext db, Guid typeId, CancellationToken ct) =>
        Set(db).CountAsync(entity => entity.ContentTypeId == typeId, ct);

    /// <summary>
    /// Standardfall: alphabetisch. Virtuell aus demselben Grund wie
    /// <see cref="FindReferencesAsync"/> — Module mit eigener Reihenfolge (Seltenheiten
    /// nach Rang) überschreiben das hier, sonst liefe ihre Fassung stillschweigend nie.
    /// </summary>
    public virtual Task<List<EntitySummary>> GetEntitiesAsync(
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

    /// <summary>
    /// Die Projektgrenze steht bewusst mit in der Abfrage: Die GUIDs kommen aus der Auswahl
    /// der Oberfläche, und ein untergeschobener Fremdschlüssel soll auch dann nichts ändern,
    /// wenn er zufällig existiert.
    /// </summary>
    public async Task<List<ContentEntity>> LoadForBulkAsync(
        GameDevManagerDbContext db, Guid projectId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
    [
        .. await Set(db)
            .Where(entity => entity.GameProjectId == projectId && ids.Contains(entity.Id))
            .ToListAsync(ct)
    ];

    public async Task<List<ContentEntity>> LoadAllAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct) =>
    [
        .. await Set(db)
            .Where(entity => entity.GameProjectId == projectId)
            .OrderBy(entity => entity.Name)
            .ToListAsync(ct)
    ];

    /// <summary>
    /// <see cref="Activator"/> statt <c>new TEntity()</c>: <see cref="ContentEntity.Name"/> ist
    /// <c>required</c>, und ein parameterloses <c>new</c> an einem Typparameter ließe der
    /// Compiler deshalb nicht zu. EF legt seine Entitäten auf demselben Weg an.
    /// </summary>
    public ContentEntity CreateNew(Guid projectId, string name)
    {
        var entity = Activator.CreateInstance<TEntity>();

        entity.GameProjectId = projectId;
        entity.Name = name;

        return entity;
    }

    /// <summary>
    /// Name und Beschreibung. Virtuell aus demselben Grund wie
    /// <see cref="FindReferencesAsync"/> — Module mit eigenen Texten (Dialogzeilen) hängen
    /// ihre Suche hier an.
    /// </summary>
    public virtual Task<List<SearchHit>> SearchAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct) =>
        Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId
                && (entity.Name.ToLower().Contains(needle)
                    || (entity.Description != null && entity.Description.ToLower().Contains(needle))))
            .OrderBy(entity => entity.Name)
            .Take(limit))
        .ToListAsync(ct);

    /// <summary>
    /// Die Textwerte der benutzerdefinierten Felder. Gesucht wird über die Entitäten des
    /// Moduls und nicht über die Feldwerte selbst: Die tragen keine Projekt-Spalte, und über
    /// den Umweg bleibt der Treffer sicher im aktuellen Projekt.
    /// </summary>
    public Task<List<SearchHit>> SearchFieldValuesAsync(
        GameDevManagerDbContext db, Guid projectId, string needle, int limit, CancellationToken ct)
    {
        // In eine lokale Variable ziehen: eine Eigenschaft der Klasse könnte EF nicht übersetzen.
        var moduleKey = ModuleKey;

        return Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId
                && db.FieldValues.Any(value => value.OwnerEntityId == entity.Id
                    && value.OwnerModuleKey == moduleKey
                    && value.TextValue != null
                    && value.TextValue.ToLower().Contains(needle)))
            .OrderBy(entity => entity.Name)
            .Take(limit))
        .ToListAsync(ct);
    }

    public async Task<SearchHit?> FindByIdAsync(
        GameDevManagerDbContext db, Guid projectId, Guid id, CancellationToken ct) =>
        await Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.Id == id && entity.GameProjectId == projectId))
        .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Gezogen wird ein zufällig gesetztes Fenster über der GUID-Reihenfolge, nicht deren
    /// Anfang: sonst regneten aus einem Modul mit hundert Einträgen immer dieselben acht, und
    /// der Rest wäre nie zu sehen. Ein Fenster statt einzeln gewürfelter Zeilen, weil keiner
    /// der vier Provider eine gemeinsame Zufallssortierung kennt — und weil GUIDs ohnehin
    /// beliebig sortieren, ist ein zusammenhängender Ausschnitt schon eine gemischte Auswahl.
    /// </summary>
    public async Task<List<SearchHit>> SampleAsync(
        GameDevManagerDbContext db, Guid projectId, int limit, CancellationToken ct)
    {
        var total = await Set(db).CountAsync(entity => entity.GameProjectId == projectId, ct);

        if (total == 0)
        {
            return [];
        }

        var offset = total > limit ? Random.Shared.Next(total - limit + 1) : 0;

        return await Project(db, Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId)
            .OrderBy(entity => entity.Id)
            .Skip(offset)
            .Take(limit))
        .ToListAsync(ct);
    }

    /// <summary>
    /// Zwei Abfragen statt einer, weil <see cref="Project"/> den Zeitstempel nicht mitführt:
    /// erst die jüngsten GUIDs samt Zeit, dann dieselbe Abbildung, die auch Suche und
    /// Startscreen benutzen. So bleibt es bei einer Umsetzung je Modul, und Untertitel wie
    /// Vorschaubild stimmen überall überein.
    /// </summary>
    public async Task<List<RecentEntry>> RecentAsync(
        GameDevManagerDbContext db, Guid projectId, int limit, CancellationToken ct)
    {
        // Nach GUID als zweitem Kriterium: bei einem Import tragen alle Entitäten denselben
        // Zeitstempel, und ohne festen zweiten Schlüssel wäre die Auswahl von Lauf zu Lauf
        // eine andere.
        var stamps = await Set(db)
            .AsNoTracking()
            .Where(entity => entity.GameProjectId == projectId)
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .ThenBy(entity => entity.Id)
            .Take(limit)
            .Select(entity => new { entity.Id, entity.UpdatedAtUtc })
            .ToListAsync(ct);

        if (stamps.Count == 0)
        {
            return [];
        }

        var ids = stamps.Select(stamp => stamp.Id).ToList();
        var hits = await Project(db, Set(db)
                .AsNoTracking()
                .Where(entity => ids.Contains(entity.Id)))
            .ToListAsync(ct);

        var byId = hits.ToDictionary(hit => hit.Id);

        return
        [
            .. stamps
                .Where(stamp => byId.ContainsKey(stamp.Id))
                .Select(stamp => new RecentEntry(byId[stamp.Id], stamp.UpdatedAtUtc))
        ];
    }

    /// <summary>
    /// Kopiert eine Entität. Welche Kind-Sammlungen es gibt, steht im EF-Modell — sie werden
    /// von dort geholt statt je Modul aufgezählt, damit ein neu hinzugekommenes Kind von
    /// selbst mitkommt. Das Umschreiben der GUIDs übernimmt <see cref="EntityDuplication"/>.
    /// </summary>
    public async Task<Guid?> DuplicateAsync(
        GameDevManagerDbContext db, Guid entityId, string name, CancellationToken ct)
    {
        IQueryable<TEntity> query = Set(db).AsNoTracking();

        foreach (var navigation in db.Model.FindEntityType(typeof(TEntity))!
                     .GetNavigations()
                     .Where(navigation => navigation.IsCollection))
        {
            query = query.Include(navigation.Name);
        }

        var original = await query.FirstOrDefaultAsync(entity => entity.Id == entityId, ct);

        return original is null
            ? null
            : (await EntityDuplication.CopyAsync(db, original, name, ct)).Id;
    }

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
    /// Erfasst den vollständigen Baum für den Papierkorb. Die Kind-Sammlungen kommen wie beim
    /// Duplizieren aus dem EF-Modell — ein neu hinzugekommenes Kind ist von selbst dabei.
    /// </summary>
    public async Task<RecycledEntity?> CaptureAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        IQueryable<TEntity> query = Set(db).AsNoTracking();

        foreach (var navigation in db.Model.FindEntityType(typeof(TEntity))!
                     .GetNavigations()
                     .Where(navigation => navigation.IsCollection))
        {
            query = query.Include(navigation.Name);
        }

        var original = await query.FirstOrDefaultAsync(entity => entity.Id == entityId, ct);

        return original is null
            ? null
            : new RecycledEntity(original.Name, await EntityDuplication.CaptureAsync(db, original, ct));
    }

    /// <inheritdoc />
    public void Restore(GameDevManagerDbContext db, string payload) =>
        EntityDuplication.Restore<TEntity>(db, payload);

    /// <summary>
    /// Standardfall: Das Modul trägt seine Texte in Name, Beschreibung und benutzerdefinierten
    /// Feldern — die kennt der <see cref="LocalizationService"/> bereits. Virtuell aus
    /// demselben Grund wie <see cref="FindReferencesAsync"/>.
    /// </summary>
    public virtual Task<List<TranslatableText>> GetTranslatableTextsAsync(
        GameDevManagerDbContext db, Guid projectId, CancellationToken ct) =>
        Task.FromResult(new List<TranslatableText>());

    /// <summary>
    /// Bildet Entitäten auf Suchtreffer ab. Je Modul eigen, weil Untertitel und Vorschaubild
    /// unterschiedlich zustande kommen — beim Item die Art, beim Rezept das Ergebnis.
    /// </summary>
    protected abstract IQueryable<SearchHit> Project(GameDevManagerDbContext db, IQueryable<TEntity> query);
}
