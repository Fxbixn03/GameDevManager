using GameDevManager.Domain.Entities;

namespace GameDevManager.Data.Services;

/// <summary>
/// Die Vererbung zwischen <b>Entitäten</b> — „Eisenschwert +1“ übernimmt jeden Feldwert des
/// „Eisenschwerts“, den es nicht selbst setzt.
/// <para>
/// Dieselbe Idee wie bei den Unterarten (<see cref="ContentType.ParentId"/>), nur eine Ebene
/// tiefer: Dort erbt eine Art die <b>Felder</b> einer anderen, hier erbt eine Entität die
/// <b>Werte</b> einer anderen. Beides läuft über eine Kette, beides verbietet Ringe.
/// </para>
/// <para>
/// Eine reine Rechenklasse ohne Datenbankzugriff — wer sie ruft, hat die Entitäten und Werte
/// schon geladen. So kann der Export sie über den gesamten Bestand auf einmal anwenden, und
/// die Bearbeitungsmaske über eine einzige Kette.
/// </para>
/// </summary>
public static class EntityInheritance
{
    /// <summary>
    /// So viele Stufen weit wird eine Kette verfolgt. Der Ring ist der eigentliche Schutz —
    /// diese Grenze fängt nur den Fall ab, dass eine Kette aus einem fremden Import länger ist
    /// als je gedacht, und verhindert, dass das Auflösen dabei hängen bleibt.
    /// </summary>
    private const int MaxDepth = 32;

    /// <summary>
    /// Die Kette der Vorbilder einer Entität, vom nächsten zum entferntesten — ohne die
    /// Entität selbst. Ein Ring bricht die Kette ab, statt endlos zu laufen: Gemeldet wird er
    /// beim Speichern (<see cref="FindCycle"/>), gelesen wird trotzdem weiter.
    /// </summary>
    public static List<Guid> ChainOf(Guid entityId, IReadOnlyDictionary<Guid, Guid?> basedOn)
    {
        var chain = new List<Guid>();
        var seen = new HashSet<Guid> { entityId };
        var current = entityId;

        while (chain.Count < MaxDepth
            && basedOn.TryGetValue(current, out var parent)
            && parent is { } parentId
            && seen.Add(parentId))
        {
            chain.Add(parentId);
            current = parentId;
        }

        return chain;
    }

    /// <summary>
    /// Prüft, ob <paramref name="entityId"/> mit dem gewünschten Vorbild einen Ring bildet —
    /// entweder direkt (sich selbst) oder über die Kette. Liefert die GUID, an der der Ring
    /// zurück auf die Entität führt, sonst <c>null</c>.
    /// </summary>
    public static Guid? FindCycle(
        Guid entityId, Guid basedOnId, IReadOnlyDictionary<Guid, Guid?> basedOn)
    {
        if (entityId == basedOnId)
        {
            return entityId;
        }

        // Gefragt ist die Kette, wie sie nach der Zuweisung aussähe — deshalb ab dem Vorbild.
        var seen = new HashSet<Guid> { entityId, basedOnId };
        var current = basedOnId;

        for (var step = 0; step < MaxDepth; step++)
        {
            if (!basedOn.TryGetValue(current, out var parent) || parent is not { } parentId)
            {
                return null;
            }

            if (parentId == entityId)
            {
                return current;
            }

            if (!seen.Add(parentId))
            {
                // Ein Ring, der die Entität nicht berührt — er ist nicht unser Fehler, aber
                // die Kette endet hier.
                return null;
            }

            current = parentId;
        }

        return null;
    }

    /// <summary>
    /// Die <b>wirksamen</b> Werte einer Entität: die eigenen, ergänzt um die des Vorbilds für
    /// jedes Feld, das sie nicht selbst setzt. Geerbte Werte tragen ihre Herkunft in
    /// <see cref="FieldValue.InheritedFromEntityId"/>.
    /// <para>
    /// Der <b>nähere</b> Wert gewinnt: Setzt die Variante ihn, gilt ihrer; sonst der des
    /// direkten Vorbilds, sonst dessen Vorbild. Alles andere wäre nicht zu begründen — man
    /// überschreibt nach unten, nicht nach oben.
    /// </para>
    /// <para>
    /// Zurückgegeben werden <b>Kopien</b>. Die geerbten Werte gehören einer anderen Entität;
    /// sie hier zu verändern schriebe in deren Datensatz.
    /// </para>
    /// </summary>
    public static Dictionary<Guid, FieldValue> Resolve(
        Guid entityId,
        string moduleKey,
        IReadOnlyDictionary<Guid, Guid?> basedOn,
        ILookup<Guid, FieldValue> valuesByOwner)
    {
        var resolved = valuesByOwner[entityId].ToDictionary(value => value.FieldDefinitionId);

        foreach (var ancestorId in ChainOf(entityId, basedOn))
        {
            foreach (var inherited in valuesByOwner[ancestorId])
            {
                if (resolved.ContainsKey(inherited.FieldDefinitionId))
                {
                    continue;
                }

                resolved[inherited.FieldDefinitionId] = CopyFrom(inherited, entityId, moduleKey, ancestorId);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Nur die geerbten Werte einer Entität — die eigenen bleiben draußen. Das ist die Sicht
    /// der Bearbeitungsmaske: Sie hat die eigenen Werte längst und will wissen, was ohne sie
    /// gälte, damit sie es ausgegraut anzeigen kann.
    /// </summary>
    public static Dictionary<Guid, FieldValue> ResolveInheritedOnly(
        Guid entityId,
        string moduleKey,
        IReadOnlyDictionary<Guid, Guid?> basedOn,
        ILookup<Guid, FieldValue> valuesByOwner)
    {
        var inheritedValues = new Dictionary<Guid, FieldValue>();

        foreach (var ancestorId in ChainOf(entityId, basedOn))
        {
            foreach (var inherited in valuesByOwner[ancestorId])
            {
                if (inheritedValues.ContainsKey(inherited.FieldDefinitionId))
                {
                    continue;
                }

                inheritedValues[inherited.FieldDefinitionId] =
                    CopyFrom(inherited, entityId, moduleKey, ancestorId);
            }
        }

        return inheritedValues;
    }

    private static FieldValue CopyFrom(FieldValue source, Guid ownerId, string moduleKey, Guid fromId)
    {
        var copy = new FieldValue
        {
            // Eine eigene GUID: Zwei Zeilen mit derselben Id in einem Export wären ein Fehler,
            // und die geerbte Zeile ist eine andere als die, von der sie stammt.
            FieldDefinitionId = source.FieldDefinitionId,
            OwnerEntityId = ownerId,
            OwnerModuleKey = moduleKey,
            InheritedFromEntityId = fromId
        };

        ContentFields.CopyValues(source, copy);
        return copy;
    }
}
