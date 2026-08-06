namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Markierung auf einer Karte: ein Punkt oder ein Bereich.
/// <para>
/// Position und Radius sind <b>relativ</b> zur Bildgröße (0 bis 1) und nicht in Pixeln. Damit
/// bleiben die Markierungen richtig, egal wie groß die Karte gerade dargestellt wird — und
/// auch dann, wenn dasselbe Bild später in höherer Auflösung neu hochgeladen wird.
/// </para>
/// <para>
/// Worauf die Markierung zeigt, steht als Modul-Schlüssel und GUID daran. Damit deckt ein
/// einziges Modell alle Fälle des Konzepts ab: der Spawn-Ort eines NPCs, die Verknüpfung auf
/// eine andere Karte und später das Gebiet einer Fraktion.
/// </para>
/// </summary>
public class MapMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MapId { get; set; }

    public GameMap? Map { get; set; }

    /// <summary>Waagerechte Lage, 0 = linker Rand, 1 = rechter Rand.</summary>
    public double X { get; set; }

    /// <summary>Senkrechte Lage, 0 = oberer Rand, 1 = unterer Rand.</summary>
    public double Y { get; set; }

    /// <summary>
    /// Radius relativ zur Bildbreite. <c>null</c> heißt Punkt; ein Wert macht daraus einen
    /// Bereich — etwa das Gebiet, in dem eine Mob-Art vorkommt.
    /// </summary>
    public double? Radius { get; set; }

    public string? Label { get; set; }

    /// <summary>Modul der Zielentität — siehe <see cref="ModuleKeys"/>. <c>null</c> bei reinen Notizen.</summary>
    public string? TargetModuleKey { get; set; }

    /// <summary>GUID der Zielentität, ohne Fremdschlüssel wie alle modulübergreifenden Verweise.</summary>
    public Guid? TargetEntityId { get; set; }

    /// <summary>
    /// Werkzeug-Asset als Symbol. Genau dafür sieht das Konzept Assets ohne Entität vor:
    /// „Marker für die Karten/Maps“.
    /// </summary>
    public Guid? IconAssetId { get; set; }

    /// <summary>Farbe als Hex-Wert; ohne Angabe wird das Akzentgelb verwendet.</summary>
    public string? Color { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Ein Bereich statt eines Punktes.</summary>
    public bool IsArea => Radius is > 0;

    /// <summary>Zeigt auf eine andere Karte — das ist die Verknüpfung aus dem Konzept.</summary>
    public bool IsMapLink => TargetModuleKey == ModuleKeys.Maps && TargetEntityId is not null;
}
