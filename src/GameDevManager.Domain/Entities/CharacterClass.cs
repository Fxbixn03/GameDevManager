namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Klasse, die auf die Spielerfigur und NPCs gemappt werden kann. Besondere und
/// passive Fähigkeiten, Startwerte und Ähnliches definiert der Nutzer als Felder an der
/// Klassen-Art — die Klasse selbst trägt strukturell nichts weiter.
/// <para>
/// <c>CharacterClass</c> statt <c>Class</c>, weil Letzteres in C# als Bezeichner ständig
/// mit dem Schlüsselwort kollidieren würde.
/// </para>
/// </summary>
public class CharacterClass : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Classes;
}
