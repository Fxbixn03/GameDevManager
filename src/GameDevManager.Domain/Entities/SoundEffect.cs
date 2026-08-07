namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Sound bzw. Musikstück. Das Konzept lässt dieses Modul offen — die Grundform hier:
/// Die Audiodateien selbst liegen als Assets an der Entität (die Positivliste der
/// Asset-Bibliothek kennt dafür Audio-Typen), Abmischung, Lautstärke oder Loop-Verhalten
/// definiert der Nutzer als Felder an der Audio-Art. Verknüpft wird der Sound aus anderen
/// Modulen über benutzerdefinierte Referenzfelder.
/// </summary>
public class SoundEffect : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Audio;
}
