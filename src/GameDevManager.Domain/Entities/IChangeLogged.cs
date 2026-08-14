namespace GameDevManager.Domain.Entities;

/// <summary>
/// Was im Änderungsprotokoll auftaucht. Jede Entität, die der Nutzer als „Sache“ wahrnimmt,
/// trägt diese vier Angaben ohnehin schon — die Schnittstelle bündelt sie nur, damit das
/// Protokoll beim Speichern nicht je Modul nachgeführt werden muss.
/// <para>
/// Bewusst nur die vier vorhandenen Mitglieder und kein neues: Eine zusätzliche Eigenschaft
/// müsste in jeder Modul-Entität aus dem EF-Modell ausgenommen werden. <c>ContentEntity</c>
/// erfüllt sie ohne eine Zeile Änderung, und die Kind-Sammlungen (Rezept-Zutaten,
/// Händler-Posten) bleiben absichtlich draußen: Sie werden mit ihrer Entität gespeichert und
/// stehen als deren Änderung im Protokoll.
/// </para>
/// </summary>
public interface IChangeLogged
{
    Guid Id { get; }

    Guid GameProjectId { get; }

    string Name { get; }

    /// <summary>Modul der Entität — siehe <see cref="ModuleKeys"/>.</summary>
    string ModuleKey { get; }
}
