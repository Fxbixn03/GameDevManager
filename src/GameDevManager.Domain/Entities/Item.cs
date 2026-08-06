namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Item des Spiels. Über Name und Beschreibung hinaus ist das Schema bewusst offen:
/// Werte wie Schaden, Gewicht oder Seltenheit definiert der Nutzer als Felder an der Item-Art,
/// einzigartige Werte als individuelle Felder direkt am Item.
/// </summary>
public class Item : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Items;
}
