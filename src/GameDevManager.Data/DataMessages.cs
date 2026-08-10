namespace GameDevManager.Data;

/// <summary>
/// Markerklasse für die Texte der Datenschicht: Validierungsmeldungen, Health-Check-Sätze und
/// die Beschriftungen, die aus den Diensten in die Oberfläche gehen.
/// <para>
/// Sie hat bewusst keinen Inhalt — <c>IStringLocalizer&lt;DataMessages&gt;</c> braucht nur einen
/// Typ, um die nebenliegende <c>DataMessages.resx</c> zu finden. Die Meldungen entstehen hier
/// und nicht in der Web-Schicht, weil die Dienste selbst prüfen; die Oberfläche reicht die
/// Meldung nur noch durch.
/// </para>
/// </summary>
public sealed class DataMessages;
