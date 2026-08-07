namespace GameDevManager.Domain.Entities;

/// <summary>
/// Ein modulübergreifendes Tag/Label. Das Konzept lässt je Tag einstellen, „in welchen
/// anderen Modulen und Bereichen sie verfügbar sind“ — das sind die <see cref="Scopes"/>;
/// ohne Eintrag gilt das Tag überall.
/// <para>
/// <c>ContentTag</c> statt <c>Tag</c>, weil die Asset-Bibliothek ihre eigenen, bewusst auf
/// Assets beschränkten <see cref="AssetTag"/>-Stichwörter hat und beides sonst ständig
/// verwechselt würde.
/// </para>
/// </summary>
public class ContentTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Anzeigefarbe als Hex-Wert, z. B. „#FFC300“. Leer heißt Standardfarbe.</summary>
    public string? Color { get; set; }

    /// <summary>Module, in denen das Tag verfügbar ist. Leer heißt: in allen.</summary>
    public List<ContentTagScope> Scopes { get; set; } = [];

    public List<ContentTagAssignment> Assignments { get; set; } = [];
}

/// <summary>Die Freigabe eines Tags für ein Modul.</summary>
public class ContentTagScope
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContentTagId { get; set; }

    public ContentTag? ContentTag { get; set; }

    /// <summary>Modul-Schlüssel — siehe <see cref="ModuleKeys"/>.</summary>
    public required string ModuleKey { get; set; }
}

/// <summary>
/// Die Zuweisung eines Tags an eine Entität. Die Entität hängt — wie überall über die
/// Modulgrenze — nur über ihre GUID daran; die zentrale Aufräumroutine entfernt sie beim Löschen.
/// </summary>
public class ContentTagAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContentTagId { get; set; }

    public ContentTag? ContentTag { get; set; }

    /// <summary>Modul der Entität — siehe <see cref="ModuleKeys"/>.</summary>
    public required string TargetModuleKey { get; set; }

    public Guid TargetEntityId { get; set; }
}
