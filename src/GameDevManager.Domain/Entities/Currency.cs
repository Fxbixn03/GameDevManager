namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Spielwährung. Das Konzept lässt beliebig viele nebeneinander zu; Händler nehmen
/// später eine davon entgegen.
/// <para>
/// Strukturell trägt die Währung nur ihr Zeichen. Es steht hier und nicht in einem
/// benutzerdefinierten Feld, weil jede Ansicht, die einen Preis zeigt, es zuverlässig
/// finden muss — genauso wie das Rezept sein Ergebnis-Item fest trägt. Alles Weitere
/// (Wechselkurs, Höchstbetrag, Fraktionszugehörigkeit) definiert der Nutzer als Felder
/// an der Währungs-Art.
/// </para>
/// </summary>
public class Currency : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Currencies;

    /// <summary>Kurzzeichen für Preisangaben, z. B. „G“, „⛁“ oder „Cr“.</summary>
    public string? Symbol { get; set; }
}
