using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Arbeit mit benutzerdefinierten Feldern, die in jedem Modul gleich abläuft: laden,
/// prüfen und speichern der Werte einer Entität.
/// <para>
/// Bewusst statische Hilfen statt einer Basisklasse — die Modul-Services rufen sie innerhalb
/// ihres eigenen DbContexts auf, sodass Stammdaten und Feldwerte in einem einzigen
/// <c>SaveChanges</c> landen.
/// </para>
/// </summary>
public static class ContentFields
{
    /// <summary>Die individuellen Felder einer Entität, fertig sortiert und mit Auswahlmöglichkeiten.</summary>
    public static async Task<List<FieldDefinition>> LoadIndividualFieldsAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var fields = await db.FieldDefinitions
            .AsNoTracking()
            .Where(f => f.OwnerEntityId == entityId)
            .Include(f => f.Options)
            .ToListAsync(ct);

        foreach (var field in fields)
        {
            field.Options = [.. field.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Label)];
        }

        return [.. fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Name)];
    }

    /// <summary>Die erfassten Werte einer Entität, nach Felddefinition abgelegt.</summary>
    public static async Task<Dictionary<Guid, FieldValue>> LoadValuesAsync(
        GameDevManagerDbContext db, Guid entityId, CancellationToken ct)
    {
        var values = await db.FieldValues
            .AsNoTracking()
            .Where(v => v.OwnerEntityId == entityId)
            .ToListAsync(ct);

        return values.ToDictionary(v => v.FieldDefinitionId);
    }

    /// <summary>Wirft, sobald ein Pflichtfeld leer geblieben ist.</summary>
    public static void ValidateRequired<TEntity>(
        ContentEditContext<TEntity> context, IStringLocalizer<DataMessages> messages)
        where TEntity : ContentEntity
    {
        foreach (var field in context.ApplicableFields.Where(f => f.IsRequired))
        {
            if (Canonicalize(field, context.ValueFor(field)).IsEmpty)
            {
                throw new ContentValidationException(messages["RequiredFieldEmpty", field.Name]);
            }
        }
    }

    /// <summary>
    /// Trägt die Werte der Maske in den DbContext ein — ohne zu speichern, damit der Aufrufer
    /// sie zusammen mit seinen Stammdaten schreibt. Davor läuft die Schreibkonflikt-Prüfung
    /// (<see cref="EnsureNotChangedElsewhereAsync"/>).
    /// <para>
    /// Werte von Feldern, die nach einem Artwechsel nicht mehr gelten, werden entfernt. Sonst
    /// bliebe unsichtbarer Inhalt in der Datenbank stehen und tauchte in Exporten und der
    /// Referenzansicht wieder auf.
    /// </para>
    /// </summary>
    public static async Task StageValuesAsync<TEntity>(
        GameDevManagerDbContext db, ContentEditContext<TEntity> context,
        IStringLocalizer<DataMessages> messages, CancellationToken ct)
        where TEntity : ContentEntity
    {
        await EnsureNotChangedElsewhereAsync(db, context, messages, ct);

        var entity = context.Entity;
        var applicable = context.ApplicableFields.ToDictionary(f => f.Id);

        var existingValues = await db.FieldValues
            .Where(v => v.OwnerEntityId == entity.Id)
            .ToListAsync(ct);

        foreach (var existing in existingValues)
        {
            if (!applicable.TryGetValue(existing.FieldDefinitionId, out var field))
            {
                db.FieldValues.Remove(existing);
                continue;
            }

            var edited = Canonicalize(field, context.ValueFor(field));
            if (edited.IsEmpty)
            {
                db.FieldValues.Remove(existing);
                continue;
            }

            CopyValues(edited, existing);
        }

        var alreadyStored = existingValues.Select(v => v.FieldDefinitionId).ToHashSet();

        foreach (var (fieldId, field) in applicable)
        {
            if (alreadyStored.Contains(fieldId))
            {
                continue;
            }

            var edited = Canonicalize(field, context.ValueFor(field));
            if (edited.IsEmpty)
            {
                continue;
            }

            var created = new FieldValue
            {
                Id = edited.Id,
                FieldDefinitionId = fieldId,
                OwnerEntityId = entity.Id,
                OwnerModuleKey = entity.ModuleKey
            };

            CopyValues(edited, created);
            db.FieldValues.Add(created);
        }
    }

    /// <summary>
    /// Die Schreibkonflikt-Erkennung: Wirft, wenn jemand anders die Entität geändert hat,
    /// seit die Maske sie geladen hat.
    /// <para>
    /// Verglichen wird <c>UpdatedAtUtc</c> — der Stand, den die Maske mit sich trägt, gegen
    /// den, der in der Datenbank steht. Ein Zeitstempel und keine <c>rowversion</c>: Die gibt
    /// es nur im SQL Server, PostgreSQL hätte <c>xmin</c>, MySQL und SQLite gar nichts. Für
    /// vier Provider mit derselben Spalte bleibt nur der Zeitstempel, den ohnehin jede
    /// <see cref="ContentEntity"/> trägt und jeder Dienst beim Speichern hochsetzt.
    /// </para>
    /// <para>
    /// Bewusst hier und nicht als eigener Aufruf in jedem der gut zwanzig Modul-Dienste: Diese
    /// Methode ist die eine Stelle, durch die alle unmittelbar vor dem Speichern laufen —
    /// dieselbe Überlegung wie bei <see cref="EntityCleanup"/>. Ein zusätzlicher Aufruf je
    /// Dienst wäre der, den ein neues Modul vergisst.
    /// </para>
    /// <para>
    /// Ist die Zeile inzwischen ganz verschwunden, ist das <b>kein</b> Konflikt: Speichern legt
    /// sie dann wieder an — das Verhalten gab es schon vorher, und ein Fehler statt der Rettung
    /// des offenen Formulars wäre die schlechtere Antwort.
    /// </para>
    /// </summary>
    public static async Task EnsureNotChangedElsewhereAsync<TEntity>(
        GameDevManagerDbContext db, ContentEditContext<TEntity> context,
        IStringLocalizer<DataMessages> messages, CancellationToken ct)
        where TEntity : ContentEntity
    {
        if (context.IsNew)
        {
            return;
        }

        var entityId = context.Entity.Id;

        var current = await db.Set<TEntity>()
            .AsNoTracking()
            .Where(entity => entity.Id == entityId)
            .Select(entity => (DateTime?)entity.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        // Verglichen wird gegen den Zeitstempel, den die Maske mitbringt. Nach jedem
        // Speichern schreiben die Modul-Dienste den neuen Stand dorthin zurück — deshalb
        // funktioniert auch das zweite Speichern aus demselben offenen Formular.
        if (current is { } stored
            && Math.Abs((stored - context.Entity.UpdatedAtUtc).TotalMilliseconds) > StorageRoundingMillis)
        {
            throw new ContentConcurrencyException(
                messages["ConcurrentEdit", context.Entity.Name, stored.ToLocalTime().ToString("g")]);
        }
    }

    /// <summary>
    /// Wie weit zwei Zeitstempel auseinanderliegen dürfen und trotzdem derselbe sind.
    /// <para>
    /// Das ist <b>kein</b> Zeitfenster für Änderungen, sondern der Rundungsfehler des
    /// Speicherns: Alle vier Provider halten Sekundenbruchteile fest (SQL Server
    /// <c>datetime2</c>, PostgreSQL und MySQL auf Mikrosekunden, SQLite als ISO-Text), aber
    /// die 100-Nanosekunden-Schritte von .NET überstehen den Weg nicht überall unverändert.
    /// Eine Millisekunde liegt weit über dieser Ungenauigkeit und weit unter allem, was zwei
    /// Menschen nacheinander tun.
    /// </para>
    /// </summary>
    private const double StorageRoundingMillis = 1;

    /// <summary>
    /// Bringt den Wert eines Stichwortfeldes auf seine kanonische Form — getrimmt, ohne
    /// Leereinträge und Dubletten (siehe <see cref="KeywordList"/>).
    /// <para>
    /// Bewusst hier und nicht erst in <see cref="CopyValues"/>: Erst danach steht fest, ob ein
    /// Wert leer ist. Eine Eingabe aus lauter Kommas trüge sonst Text, hätte aber kein einziges
    /// Stichwort — ein Pflichtfeld gälte als ausgefüllt und in der Datenbank landete eine Zeile
    /// ohne Inhalt.
    /// </para>
    /// </summary>
    private static FieldValue Canonicalize(FieldDefinition field, FieldValue value)
    {
        if (field.IsKeywordField)
        {
            value.TextValue = KeywordList.Normalize(value.TextValue);
        }

        return value;
    }

    /// <summary>
    /// Überträgt die Wertspalten, ohne Id und Zuordnung anzufassen. <c>internal</c>, weil die
    /// Massenbearbeitung denselben Wert auf viele Entitäten überträgt und dabei genau diese
    /// Spalten meint — zweimal aufgezählt liefen sie beim nächsten neuen Feldtyp auseinander.
    /// </summary>
    internal static void CopyValues(FieldValue source, FieldValue target)
    {
        target.TextValue = string.IsNullOrWhiteSpace(source.TextValue) ? null : source.TextValue.Trim();
        target.NumberValue = source.NumberValue;
        target.BooleanValue = source.BooleanValue;
        target.DateValue = source.DateValue;
        target.ReferenceValue = source.ReferenceValue;
        target.OptionId = source.OptionId;
    }
}
