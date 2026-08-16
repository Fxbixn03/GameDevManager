namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Cutscene. Das Konzept lässt dieses Modul offen — die Grundform hier: ein
/// Storyboard aus geordneten Einstellungen (<see cref="CutsceneShot"/>) plus Verknüpfungen
/// zur Story und zu einem Dialog, beides GUID-Referenzen ohne Fremdschlüssel über die
/// Modulgrenze. Auflösung, Kameraführung und Ähnliches definiert der Nutzer als Felder an
/// der Cutscene-Art.
/// </summary>
public class Cutscene : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Cutscenes;

    /// <summary>Der Story-Abschnitt, zu dem die Cutscene gehört.</summary>
    public Guid? StoryEntryId { get; set; }

    /// <summary>Der Dialog, der in der Cutscene gesprochen wird.</summary>
    public Guid? DialogueId { get; set; }

    public List<CutsceneShot> Shots { get; set; } = [];
}

/// <summary>
/// Eine Einstellung des Storyboards: was in dieser Szene passiert.
/// <para>
/// Das <b>Skizzenbild</b> hängt wie überall über die GUID an der Einstellung — ein
/// <see cref="Asset"/> mit <see cref="Asset.OwnerEntityId"/> = <see cref="Id"/>. Es braucht
/// dafür keine Spalte; die Einstellung hat eine eigene GUID, und genau darauf ist die
/// Asset-Anbindung ausgelegt. Beim Löschen räumt der <c>CutsceneService</c> mit ab.
/// </para>
/// </summary>
public class CutsceneShot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CutsceneId { get; set; }

    public Cutscene? Cutscene { get; set; }

    public required string Text { get; set; }

    /// <summary>
    /// Dauer der Einstellung in Sekunden. <c>double</c> wie überall im Haus, weil SQLite
    /// keinen Dezimaltyp kennt — und halbe Sekunden sind im Schnitt der Normalfall.
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Kameranotiz — „Totale, langsamer Zoom“. Freitext, wie die Stimmung der Story.</summary>
    public string? CameraNote { get; set; }

    public int SortOrder { get; set; }
}
