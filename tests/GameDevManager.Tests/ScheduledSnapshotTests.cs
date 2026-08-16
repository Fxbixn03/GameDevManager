using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
using Xunit;

namespace GameDevManager.Tests;

/// <summary>
/// Exportstände nach Zeitplan: die Sicherung, die auch dann entsteht, wenn niemand daran
/// gedacht hat. Der interessante Teil ist die Zurückhaltung — ohne Änderung kein Stand, sonst
/// füllte sich das Verzeichnis Nacht für Nacht mit identischen Archiven.
/// </summary>
public class ScheduledSnapshotTests
{
    private static async Task<Guid> SaveItemAsync(TestDatabase test, string name)
    {
        var items = test.GetService<ItemService>();
        var context = await items.LoadForEditAsync(test.ProjectId, null);

        context!.Entity.Name = name;
        await items.SaveItemAsync(context);

        return context.Entity.Id;
    }

    [Fact]
    public async Task Ein_Lauf_legt_je_Projekt_einen_Stand_an()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Schwert");

        var snapshots = test.GetService<ExportSnapshotService>();

        Assert.Equal(1, await snapshots.CreateScheduledAsync(includeAssets: false));
        Assert.Single(snapshots.List(test.ProjectId));
    }

    [Fact]
    public async Task Ohne_Aenderung_entsteht_kein_zweiter_Stand()
    {
        using var test = new TestDatabase();
        await SaveItemAsync(test, "Schwert");

        var snapshots = test.GetService<ExportSnapshotService>();
        await snapshots.CreateScheduledAsync(includeAssets: false);

        // Nichts passiert — dann gibt es auch nichts zu sichern.
        Assert.Equal(0, await snapshots.CreateScheduledAsync(includeAssets: false));
        Assert.Single(snapshots.List(test.ProjectId));

        // Nach einer Änderung dagegen schon.
        await SaveItemAsync(test, "Axt");

        Assert.Equal(1, await snapshots.CreateScheduledAsync(includeAssets: false));
        Assert.Equal(2, snapshots.List(test.ProjectId).Count);
    }

    [Fact]
    public void Der_naechste_Termin_liegt_immer_in_der_Zukunft()
    {
        var time = new TimeOnly(3, 0);

        // Vor der Uhrzeit: heute noch.
        Assert.Equal(
            TimeSpan.FromHours(2),
            ExportStorageOptions.UntilNext(time, new DateTime(2026, 5, 1, 1, 0, 0)));

        // Danach: morgen.
        Assert.Equal(
            TimeSpan.FromHours(23),
            ExportStorageOptions.UntilNext(time, new DateTime(2026, 5, 1, 4, 0, 0)));

        // Genau auf der Uhrzeit gilt als „schon gelaufen“ — sonst startete der Dienst in
        // dieser Minute sofort los.
        Assert.Equal(
            TimeSpan.FromDays(1),
            ExportStorageOptions.UntilNext(time, new DateTime(2026, 5, 1, 3, 0, 0)));
    }

    [Fact]
    public void Eine_kaputte_Uhrzeit_schaltet_den_Zeitplan_ab()
    {
        Assert.Null(new ExportStorageOptions { ScheduleTime = "abends" }.DailyTime);
        Assert.Null(new ExportStorageOptions { ScheduleTime = "" }.DailyTime);
        Assert.False(new ExportStorageOptions().HasSchedule);

        Assert.Equal(new TimeOnly(3, 30), new ExportStorageOptions { ScheduleTime = "03:30" }.DailyTime);
    }
}
