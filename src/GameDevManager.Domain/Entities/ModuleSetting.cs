namespace GameDevManager.Domain.Entities;

/// <summary>
/// Switches a module on or off per project. Only deviations from the default are stored:
/// no row means the module is enabled, so new modules are visible right away without seeding.
/// Disabling a module only hides its surface — the module's content stays in the database.
/// </summary>
public class ModuleSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameProjectId { get; set; }

    public GameProject? GameProject { get; set; }

    public required string ModuleKey { get; set; }

    public bool IsEnabled { get; set; } = true;
}
