using GameDevManager.Domain.Curves;

namespace GameDevManager.Web.Components.Content;

/// <summary>
/// Eine Linie im Kurven-Diagramm. Mehrere davon liegen übereinander — die bearbeitete Kurve
/// zuerst, die Vergleichskurven danach.
/// </summary>
/// <param name="Name">Beschriftung in der Legende.</param>
/// <param name="Points">Die ausgerechnete Wertetabelle (<see cref="CurveDefinition.Sample"/>).</param>
public sealed record CurveSeries(string Name, IReadOnlyList<CurvePoint> Points);
