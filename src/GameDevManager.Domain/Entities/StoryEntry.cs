namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Abschnitt der Storyline. Das Konzept will die Story schreiben und „in einem
/// Zeitstreifen“ anzeigen — die Reihenfolge kommt aus <see cref="SortOrder"/>, nicht aus
/// Datumswerten, weil Spielzeit selten echte Zeit ist.
/// <para>
/// Der eigentliche Text liegt in <see cref="Body"/> und ist bewusst ohne Längengrenze;
/// die geerbte Beschreibung bleibt die Kurzfassung für Listen und Suche.
/// </para>
/// </summary>
public class StoryEntry : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Story;

    /// <summary>Position im Zeitstreifen, klein = früh.</summary>
    public int SortOrder { get; set; }

    /// <summary>Der ausgeschriebene Story-Text dieses Abschnitts.</summary>
    public string? Body { get; set; }

    /// <summary>Die Stimmung der Szene — „bedrückend“, „ausgelassen“. Freitext.</summary>
    public string? Mood { get; set; }

    /// <summary>
    /// Datum innerhalb der Spielwelt — Freitext („3. Tag der Aschewoche“), kein echtes
    /// Datum: Spielzeit ist selten echte Zeit, die Reihenfolge trägt weiter <see cref="SortOrder"/>.
    /// </summary>
    public string? GameDate { get; set; }

    /// <summary>Wie lange die Szene spielt — ebenfalls Freitext („eine Nacht“).</summary>
    public string? Duration { get; set; }

    /// <summary>Der Ort als Freitext; die Karten-Verknüpfung daneben ist die präzise Form.</summary>
    public string? Location { get; set; }

    /// <summary>Karte des Schauplatzes. GUID-Referenz über die Modulgrenze, ohne Fremdschlüssel.</summary>
    public Guid? TargetMapId { get; set; }

    /// <summary>
    /// Eine Markierung auf <see cref="TargetMapId"/> — die genaue Position der Szene.
    /// Ohne Markierung steht die Karte allein für den Schauplatz.
    /// </summary>
    public Guid? TargetMapMarkerId { get; set; }

    public List<StoryParticipant> Participants { get; set; } = [];

    public List<StoryLink> Links { get; set; } = [];
}

/// <summary>
/// Eine Verknüpfung zu einer anderen Szene — „spielt parallel zu“, „führt später zu“. Die
/// Bedeutung steht als freies Etikett daran; das Ziel ist wie überall eine GUID-Referenz
/// ohne Fremdschlüssel, der <c>StoryService</c> räumt eingehende Verknüpfungen beim
/// Löschen eines Abschnitts selbst ab.
/// </summary>
public class StoryLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoryEntryId { get; set; }

    public StoryEntry? StoryEntry { get; set; }

    /// <summary>Der verknüpfte Abschnitt.</summary>
    public Guid TargetEntryId { get; set; }

    /// <summary>Wozu die Verknüpfung da ist — optionales Etikett („Rückblende“).</summary>
    public string? Label { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// Eine an einem Story-Abschnitt beteiligte Entität — im Konzept: „welche NPCs beteiligt
/// sind und welche Fraktionen/Dörfer und Locations auf der Karte“. Modul + GUID statt
/// Fremdschlüssel, damit jede Art von Entität teilnehmen kann.
/// </summary>
public class StoryParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoryEntryId { get; set; }

    public StoryEntry? StoryEntry { get; set; }

    /// <summary>Modul der beteiligten Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string TargetModuleKey { get; set; }

    public Guid TargetEntityId { get; set; }

    public int SortOrder { get; set; }
}
