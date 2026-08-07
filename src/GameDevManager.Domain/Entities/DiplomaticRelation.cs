namespace GameDevManager.Domain.Entities;

/// <summary>
/// Wie zwei Fraktionen zueinander stehen. Die Stufen decken die Fälle des Konzepts ab —
/// „Freundschaften/Allianzen oder auch Feindschaften“ — und geben dem Graphen seine Farbe.
/// </summary>
public enum DiplomaticStance
{
    /// <summary>Fester Zusammenschluss, gemeinsame Sache.</summary>
    Alliance = 0,

    /// <summary>Freundlich gesinnt, aber ohne Bündnis.</summary>
    Friendship = 1,

    /// <summary>Kein besonderes Verhältnis.</summary>
    Neutral = 2,

    /// <summary>Feindselig, aber (noch) ohne offenen Konflikt.</summary>
    Hostility = 3,

    /// <summary>Offener Krieg.</summary>
    War = 4
}

/// <summary>
/// Eine diplomatische Beziehung zwischen zwei Fraktionen. Die Beziehung ist ungerichtet —
/// wer als A und wer als B steht, hat keine Bedeutung.
/// <para>
/// Beide Fraktionen hängen über ihre GUID daran, ohne Fremdschlüssel über die Modulgrenze.
/// Einzelheiten wie Vertragsdauer oder Tribut definiert der Nutzer als Felder an der
/// Beziehungs-Art.
/// </para>
/// </summary>
public class DiplomaticRelation : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Diplomacy;

    public Guid FactionAId { get; set; }

    public Guid FactionBId { get; set; }

    public DiplomaticStance Stance { get; set; } = DiplomaticStance.Neutral;
}
