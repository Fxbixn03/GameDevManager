namespace GameDevManager.Web.Services;

/// <summary>
/// Hält die Reihenfolge der Modulleiste in der Topbar fest — installationsweit, aus demselben
/// Grund wie die <see cref="AppearanceSelection"/>: Das Tool wird self-hosted von einer Person
/// betrieben, und die Leiste ist Sache des Bildschirms, nicht des Spielprojekts.
/// <para>
/// Der Startwert kommt aus der Konfiguration (<c>Topbar:ModuleOrder</c>, geschrieben nach
/// <c>appsettings.Local.json</c>); ohne Eintrag gilt die Reihenfolge aus der
/// <see cref="ModuleRegistry"/>. Gespeichert wird eine kommagetrennte Schlüsselliste und keine
/// Datenbanktabelle — die verlangte eine Migration in allen vier Providern für eine Angabe,
/// die kein Projektinhalt ist und deshalb auch nicht in den Export gehört.
/// </para>
/// </summary>
public class TopbarSelection(IConfiguration configuration, LocalSettingsFile settings)
{
    private IReadOnlyList<string> _order = Normalize(
        (configuration["Topbar:ModuleOrder"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Meldet eine geänderte Reihenfolge an die offenen Ansichten — die Leiste steht auf jeder
    /// Seite, und wer sie in den Einstellungen umsortiert, will sie sofort umsortiert sehen.
    /// Wie beim <c>WhiteboardNotifier</c> erreicht das Ereignis alle Kreise; die Empfänger
    /// marshallen mit <c>InvokeAsync</c> auf ihren eigenen.
    /// </summary>
    public event Action? Changed;

    /// <summary>Die gespeicherte Reihenfolge; leer, solange nichts umsortiert wurde.</summary>
    public IReadOnlyList<string> Order => _order;

    /// <summary>
    /// Bringt Module in die gespeicherte Reihenfolge. <see cref="Enumerable.OrderBy{T, TKey}"/>
    /// ist stabil: Module ohne Eintrag — nachträglich hinzugekommene — behalten ihre Reihenfolge
    /// aus der Registry und stehen hinten.
    /// </summary>
    public IReadOnlyList<ModuleDefinition> Sort(IEnumerable<ModuleDefinition> modules) =>
        [.. modules.OrderBy(m => IndexOf(m.Id))];

    public async Task SetOrderAsync(IEnumerable<string> moduleKeys, CancellationToken ct = default)
    {
        _order = Normalize(moduleKeys);
        await settings.WriteModuleOrderAsync(_order, ct);
        Changed?.Invoke();
    }

    /// <summary>Zurück zur Reihenfolge der Registry — ein leerer Eintrag ist genau das.</summary>
    public Task ResetAsync(CancellationToken ct = default) => SetOrderAsync([], ct);

    private int IndexOf(string moduleKey)
    {
        for (var i = 0; i < _order.Count; i++)
        {
            if (string.Equals(_order[i], moduleKey, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// Unbekannte Schlüssel und Dubletten fallen heraus: In der Datei steht sonst irgendwann
    /// ein Modul, das es nicht mehr gibt, und die Reihenfolge wäre nicht mehr zu deuten.
    /// </summary>
    private static IReadOnlyList<string> Normalize(IEnumerable<string> moduleKeys)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var key in moduleKeys)
        {
            var module = ModuleRegistry.Find(key.Trim());
            if (module is not null && seen.Add(module.Id))
            {
                result.Add(module.Id);
            }
        }

        return result;
    }
}
