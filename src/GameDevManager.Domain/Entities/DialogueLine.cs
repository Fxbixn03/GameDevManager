namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine gesprochene Zeile. Bei einer Sprechblase steht sie für sich, bei einem Gespräch ist
/// sie ein Knoten im Verlauf.
/// </summary>
public class DialogueLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DialogueId { get; set; }

    public Dialogue? Dialogue { get; set; }

    /// <summary>
    /// Wer spricht. <c>null</c> heißt: der Spieler — er ist keine Entität des NPC-Moduls und
    /// bekommt deshalb keine GUID.
    /// </summary>
    public Guid? SpeakerNpcId { get; set; }

    public required string Text { get; set; }

    public int SortOrder { get; set; }

    public List<DialogueChoice> Choices { get; set; } = [];
}

/// <summary>
/// Eine Antwortmöglichkeit an einer Zeile. Sie führt zur nächsten Zeile oder beendet das
/// Gespräch.
/// </summary>
public class DialogueChoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DialogueLineId { get; set; }

    public DialogueLine? Line { get; set; }

    public required string Text { get; set; }

    /// <summary>Wohin es weitergeht. <c>null</c> beendet das Gespräch.</summary>
    public Guid? NextLineId { get; set; }

    public int SortOrder { get; set; }
}
