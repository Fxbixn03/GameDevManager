namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Spielwährung. Das Konzept lässt beliebig viele nebeneinander zu; Händler nehmen
/// später eine davon entgegen.
/// <para>
/// Strukturell trägt die Währung nur ihr Zeichen. Es steht hier und nicht in einem
/// benutzerdefinierten Feld, weil jede Ansicht, die einen Preis zeigt, es zuverlässig
/// finden muss — genauso wie das Rezept sein Ergebnis-Item fest trägt. Alles Weitere
/// (Höchstbetrag, Fraktionszugehörigkeit) definiert der Nutzer als Felder an der
/// Währungs-Art.
/// </para>
/// </summary>
public class Currency : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Currencies;

    /// <summary>Kurzzeichen für Preisangaben, z. B. „G“, „⛁“ oder „Cr“.</summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Wechselkurs in einer gedachten Grundeinheit — wie viel eine Einheit dieser Währung
    /// wert ist. <c>1</c> heißt „ist selbst die Grundeinheit“.
    /// <para>
    /// Steht hier und nicht in einem Feld der Art, obwohl das Konzept es dort vermutete: Die
    /// Wirtschafts-Prüfung muss Preise in verschiedenen Währungen vergleichen können, und
    /// dafür braucht sie den Kurs zuverlässig — dieselbe Überlegung wie beim
    /// <see cref="Symbol"/>. Ein benutzerdefiniertes Feld fände sie nicht.
    /// </para>
    /// <para>
    /// <c>double</c> wie überall im Haus: SQLite kennt keinen Dezimaltyp.
    /// </para>
    /// </summary>
    public double ExchangeRate { get; set; } = 1;
}
