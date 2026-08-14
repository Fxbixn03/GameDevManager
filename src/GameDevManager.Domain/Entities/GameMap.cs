namespace GameDevManager.Domain.Entities;

/// <summary>
/// Eine Karte des Spiels — Weltkarte, Höhle, Hausgrundriss.
/// <para>
/// Das Kartenbild ist das primäre Sprite der Karte und kommt damit aus der Asset-Bibliothek;
/// eine eigene Bildspalte gäbe es sonst zweimal. Über weitere Sprites lassen sich Varianten
/// hinterlegen (Tag/Nacht, mit und ohne Beschriftung) und per Icon-Wahl umschalten.
/// </para>
/// </summary>
public class GameMap : ContentEntity
{
    public override string ModuleKey => ModuleKeys.Maps;

    public List<MapMarker> Markers { get; set; } = [];

    public List<MapLayer> Layers { get; set; } = [];
}
