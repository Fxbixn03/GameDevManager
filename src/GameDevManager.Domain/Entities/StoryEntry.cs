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

    public List<StoryParticipant> Participants { get; set; } = [];
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
