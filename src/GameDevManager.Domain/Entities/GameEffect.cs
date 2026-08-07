namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein Effekt und seine Wirkung — das Konzept-Beispiel: „Verbrennung — das Ziel erleidet
/// X Brandschaden für X Sekunden“. Schadenswert, Dauer und Ähnliches definiert der Nutzer
/// als Felder an der Effekt-Art, weil jede Wirkung andere Größen braucht.
/// <para>
/// Die Zuweisung an Items („z. B. einem Feuerschwert“) liegt als eigene Liste am Effekt;
/// die Items hängen über ihre GUID daran, ohne Fremdschlüssel über die Modulgrenze.
/// Der Präfix im Namen folgt <see cref="GameMap"/> und <see cref="GameEvent"/>.
/// </para>
/// </summary>
public class GameEffect : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Effects;

    public List<EffectAssignment> Assignments { get; set; } = [];
}

/// <summary>Die Zuweisung eines Effekts an ein Item.</summary>
public class EffectAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameEffectId { get; set; }

    public GameEffect? GameEffect { get; set; }

    public Guid ItemId { get; set; }

    public int SortOrder { get; set; }
}
