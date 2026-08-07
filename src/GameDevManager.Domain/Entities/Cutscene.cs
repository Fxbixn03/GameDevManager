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

/// <summary>Eine Einstellung des Storyboards: was in dieser Szene passiert.</summary>
public class CutsceneShot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CutsceneId { get; set; }

    public Cutscene? Cutscene { get; set; }

    public required string Text { get; set; }

    public int SortOrder { get; set; }
}
