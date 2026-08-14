using GameDevManager.Data.Services;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Die Schreibkonflikt-Erkennung. Mit echtem Mehrbenutzerbetrieb kann dieselbe Entität in zwei
/// Masken offen sein — ohne Prüfung gewönne stillschweigend, wer zuletzt speichert.
/// <para>
/// Geprüft wird über <see cref="ItemService"/> stellvertretend für alle Modul-Dienste: Die
/// Prüfung sitzt in <see cref="ContentFields.StageValuesAsync"/>, durch das jeder von ihnen
/// unmittelbar vor dem Speichern läuft.
/// </para>
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task Wer_auf_einem_veralteten_Stand_speichert_bekommt_eine_Meldung()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Eisenschwert";
        await items.SaveItemAsync(context);

        // Zwei Masken auf derselben Entität.
        var first = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        var second = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);

        first!.Entity.Description = "Von Anna.";
        await items.SaveItemAsync(first);

        second!.Entity.Description = "Von Bruno.";
        var conflict = await Assert.ThrowsAsync<ContentConcurrencyException>(
            () => items.SaveItemAsync(second));

        // Die Meldung nennt die Entität — sonst weiß man nicht, welche der offenen Masken es ist.
        Assert.Contains("Eisenschwert", conflict.Message);

        // Der fremde Stand bleibt stehen; nichts wurde stillschweigend überschrieben.
        var stored = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        Assert.Equal("Von Anna.", stored!.Entity.Description);
    }

    [Fact]
    public async Task Ein_Schreibkonflikt_ist_ein_fachlicher_Fehler_und_landet_in_der_Oberflaeche()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Fackel";
        await items.SaveItemAsync(context);

        var stale = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);

        var fresh = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        fresh!.Entity.Name = "Grubenlampe";
        await items.SaveItemAsync(fresh);

        stale!.Entity.Name = "Kerze";

        // Abgeleitet von ContentValidationException, damit jede Maske sie ohne Änderung
        // durchreicht — die fangen fachliche Fehler ohnehin schon ab.
        await Assert.ThrowsAsync<ContentConcurrencyException>(() => items.SaveItemAsync(stale));
        await Assert.ThrowsAnyAsync<ContentValidationException>(() => items.SaveItemAsync(stale));
    }

    [Fact]
    public async Task Zweimal_aus_derselben_Maske_speichern_bleibt_moeglich()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Trank";
        await items.SaveItemAsync(context);

        // Nach jedem Speichern schreiben die Dienste den neuen Stand in die Maske zurück —
        // sonst meldete der zweite Klick einen Konflikt mit einem selbst.
        context.Entity.Description = "Heilt 20 Punkte.";
        await items.SaveItemAsync(context);

        context.Entity.Description = "Heilt 30 Punkte.";
        await items.SaveItemAsync(context);

        var stored = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        Assert.Equal("Heilt 30 Punkte.", stored!.Entity.Description);
    }

    [Fact]
    public async Task Eine_inzwischen_geloeschte_Entitaet_ist_kein_Konflikt()
    {
        using var test = new TestDatabase();
        var items = test.GetService<ItemService>();

        var context = await items.LoadForEditAsync(test.ProjectId, null);
        context!.Entity.Name = "Kompass";
        await items.SaveItemAsync(context);

        var open = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        await items.DeleteItemAsync(context.Entity.Id);

        // Speichern legt sie wieder an — ein Fehler statt der Rettung des offenen Formulars
        // wäre die schlechtere Antwort.
        open!.Entity.Description = "Zeigt nach Norden.";
        await items.SaveItemAsync(open);

        var stored = await items.LoadForEditAsync(test.ProjectId, context.Entity.Id);
        Assert.Equal("Zeigt nach Norden.", stored!.Entity.Description);
    }
}
