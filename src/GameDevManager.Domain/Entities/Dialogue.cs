namespace GameDevManager.Domain.Entities;

/// <summary>
/// Die beiden Formen, die das Konzept für Dialoge nennt.
/// </summary>
public enum DialogueKind
{
    /// <summary>
    /// Sprechblasen, die in der Open World zufällig bei einem NPC erscheinen. Die Zeilen stehen
    /// unabhängig nebeneinander, es gibt keinen Verlauf und keine Antworten.
    /// </summary>
    Bark = 0,

    /// <summary>
    /// Ein geführtes Gespräch mit Verlauf und Antwortmöglichkeiten.
    /// </summary>
    Conversation = 1
}

/// <summary>
/// Ein Dialog. Beteiligt sein können ein NPC und der Spieler, mehrere NPCs untereinander oder
/// mehrere NPCs zusammen mit dem Spieler — deshalb sind die Beteiligten eine Liste und der
/// Spieler ein eigener Schalter.
/// <para>
/// Die Klasse heißt <c>Dialogue</c> und nicht <c>Dialog</c>, weil sonst der zugehörige Dienst
/// mit <c>MudBlazor.DialogService</c> kollidierte. In der Oberfläche heißt es weiterhin „Dialog“.
/// </para>
/// </summary>
public class Dialogue : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Dialogs;

    public DialogueKind Kind { get; set; } = DialogueKind.Conversation;

    /// <summary>Der Spieler ist beteiligt. Bei einem Gespräch zwischen NPCs ist das <c>false</c>.</summary>
    public bool IncludesPlayer { get; set; } = true;

    public List<DialogueParticipant> Participants { get; set; } = [];

    public List<DialogueLine> Lines { get; set; } = [];
}

/// <summary>Ein am Dialog beteiligter NPC.</summary>
public class DialogueParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DialogueId { get; set; }

    public Dialogue? Dialogue { get; set; }

    /// <summary>GUID-Referenz auf den NPC, ohne Fremdschlüssel über die Modulgrenze.</summary>
    public Guid NpcId { get; set; }

    public int SortOrder { get; set; }
}
