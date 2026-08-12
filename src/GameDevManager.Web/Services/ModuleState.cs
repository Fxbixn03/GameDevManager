using GameDevManager.Data.Services;

namespace GameDevManager.Web.Services;

/// <summary>
/// Per-circuit view of which modules are switched on. The topbar loads it once; the settings
/// dialog updates it and raises <see cref="Changed"/> so topbar, dashboard and search follow
/// without a reload. Before the state is loaded every module counts as enabled — the strip
/// never flickers empty while the first query runs.
/// </summary>
public class ModuleState(ModuleSettingsService settings, ProjectContext project)
{
    private HashSet<string>? _disabled;

    public event Action? Changed;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        _disabled ??= await settings.GetDisabledModuleKeysAsync(await project.GetCurrentIdAsync(ct), ct);
    }

    public bool IsEnabled(string moduleKey) => _disabled is null || !_disabled.Contains(moduleKey);

    public bool IsEnabled(ModuleDefinition module) => IsEnabled(module.Id);

    public IEnumerable<ModuleDefinition> EnabledModules => ModuleRegistry.All.Where(IsEnabled);

    public async Task SetEnabledAsync(ModuleDefinition module, bool enabled, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await settings.SetModuleEnabledAsync(await project.GetCurrentIdAsync(ct), module.Id, enabled, ct);

        if (enabled)
        {
            _disabled!.Remove(module.Id);
        }
        else
        {
            _disabled!.Add(module.Id);
        }

        Changed?.Invoke();
    }
}
