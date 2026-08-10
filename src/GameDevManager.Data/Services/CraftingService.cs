using GameDevManager.Domain;
using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Data.Services;

/// <summary>
/// Crafting-Rezepte: lesen, schreiben und als Baum auflösen.
/// </summary>
public class CraftingService(
    IDbContextFactory<GameDevManagerDbContext> factory,
    ContentTypeService contentTypes,
    AssetService assets,
    IStringLocalizer<DataMessages> messages)
{
    /// <summary>
    /// Wie tief der Crafting-Baum aufgelöst wird. Reine Notbremse — echte Rezeptketten sind
    /// weit flacher, und Zyklen fängt die Pfadprüfung ohnehin ab.
    /// </summary>
    private const int MaximumTreeDepth = 12;

    // --------------------------------------------------------------------------- Übersicht

    public async Task<List<RecipeListRow>> GetRecipesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Recipes
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId)
            .OrderBy(r => r.Name)
            .Select(r => new RecipeListRow(
                r.Id,
                r.Name,
                r.ContentTypeId,
                r.ContentType!.Name,
                r.OutputItemId,
                db.Items.Where(i => i.Id == r.OutputItemId).Select(i => i.Name).FirstOrDefault(),
                r.OutputQuantity,
                db.Assets
                    .Where(a => a.OwnerEntityId == r.OutputItemId && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault(),
                r.Ingredients.Count,
                r.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>Die Rezepte, in denen ein Item als Ergebnis oder als Zutat vorkommt.</summary>
    public async Task<(List<RecipeListRow> Produces, List<RecipeListRow> Consumes)> GetRecipesForItemAsync(
        Guid projectId, Guid itemId, CancellationToken ct = default)
    {
        var all = await GetRecipesAsync(projectId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var consumingIds = await db.RecipeIngredients
            .AsNoTracking()
            .Where(i => i.ItemId == itemId)
            .Select(i => i.RecipeId)
            .Distinct()
            .ToListAsync(ct);

        return (
            [.. all.Where(row => row.OutputItemId == itemId)],
            [.. all.Where(row => consumingIds.Contains(row.Id))]);
    }

    // ------------------------------------------------------------------------- Bearbeiten

    public async Task<ContentEditContext<Recipe>?> LoadForEditAsync(
        Guid projectId, Guid? recipeId, CancellationToken ct = default)
    {
        var types = await contentTypes.GetTypesAsync(projectId, ModuleKeys.Crafting, ct);

        if (recipeId is null)
        {
            return new ContentEditContext<Recipe>
            {
                Entity = new Recipe { GameProjectId = projectId, Name = string.Empty },
                IsNew = true,
                AvailableTypes = types,
                IndividualFields = [],
                Values = []
            };
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var recipe = await db.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.GameProjectId == projectId, ct);

        if (recipe is null)
        {
            return null;
        }

        recipe.Ingredients = [.. recipe.Ingredients.OrderBy(i => i.SortOrder)];

        return new ContentEditContext<Recipe>
        {
            Entity = recipe,
            IsNew = false,
            AvailableTypes = types,
            IndividualFields = await ContentFields.LoadIndividualFieldsAsync(db, recipe.Id, ct),
            Values = await ContentFields.LoadValuesAsync(db, recipe.Id, ct)
        };
    }

    public async Task SaveRecipeAsync(ContentEditContext<Recipe> context, CancellationToken ct = default)
    {
        var recipe = context.Entity;

        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            throw new ContentValidationException(messages["RecipeNameRequired"]);
        }

        if (recipe.OutputQuantity < 1)
        {
            throw new ContentValidationException(messages["RecipeOutputAtLeastOne"]);
        }

        if (recipe.Ingredients.Any(ingredient => ingredient.Quantity < 1))
        {
            throw new ContentValidationException(messages["RecipeIngredientQuantity"]);
        }

        var duplicate = recipe.Ingredients
            .GroupBy(ingredient => ingredient.ItemId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ContentValidationException(messages["RecipeIngredientDuplicate"]);
        }

        ContentFields.ValidateRequired(context, messages);

        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var stored = await db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id, ct);

        if (stored is null)
        {
            stored = new Recipe
            {
                Id = recipe.Id,
                GameProjectId = recipe.GameProjectId,
                Name = recipe.Name.Trim(),
                CreatedAtUtc = now
            };

            db.Recipes.Add(stored);
        }

        stored.ContentTypeId = recipe.ContentTypeId;
        stored.Name = recipe.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(recipe.Description) ? null : recipe.Description.Trim();
        stored.OutputItemId = recipe.OutputItemId;
        stored.OutputQuantity = recipe.OutputQuantity;
        stored.UpdatedAtUtc = now;

        SyncIngredients(db, stored, recipe);

        await ContentFields.StageValuesAsync(db, context, ct);
        await db.SaveChangesAsync(ct);

        recipe.CreatedAtUtc = stored.CreatedAtUtc;
        recipe.UpdatedAtUtc = stored.UpdatedAtUtc;
        recipe.Name = stored.Name;
        recipe.Description = stored.Description;
    }

    private static void SyncIngredients(GameDevManagerDbContext db, Recipe stored, Recipe incoming)
    {
        var wanted = incoming.Ingredients;
        var wantedIds = wanted.Select(ingredient => ingredient.Id).ToHashSet();

        // Nur aus der Navigationsliste entfernen. Der Fremdschlüssel auf das Rezept ist
        // pflicht, also löscht EF die Waise von selbst — zusätzlich db.Remove aufzurufen
        // erzeugte einen zweiten DELETE für dieselbe Zeile.
        foreach (var obsolete in stored.Ingredients.Where(i => !wantedIds.Contains(i.Id)).ToList())
        {
            stored.Ingredients.Remove(obsolete);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var ingredient = wanted[index];
            var target = stored.Ingredients.FirstOrDefault(i => i.Id == ingredient.Id);

            if (target is null)
            {
                // Ausdrücklich über das DbSet und nicht über die Navigationsliste: die Zutat
                // bringt ihre GUID bereits mit, und EF hielte sie beim Anhängen an ein
                // bestehendes Rezept für einen vorhandenen Datensatz — es entstünde ein
                // UPDATE auf eine Zeile, die es noch gar nicht gibt.
                db.RecipeIngredients.Add(new RecipeIngredient
                {
                    Id = ingredient.Id,
                    RecipeId = stored.Id,
                    ItemId = ingredient.ItemId,
                    Quantity = ingredient.Quantity,
                    SortOrder = index
                });
            }
            else
            {
                target.ItemId = ingredient.ItemId;
                target.Quantity = ingredient.Quantity;
                target.SortOrder = index;
            }
        }
    }

    /// <summary>Löscht ein Rezept mit Zutaten, Feldwerten, individuellen Feldern und Sprites.</summary>
    public async Task DeleteRecipeAsync(Guid recipeId, CancellationToken ct = default)
    {
        await assets.DeleteForOwnerAsync(recipeId, ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await EntityCleanup.DeleteForEntityAsync(db, recipeId, ct);

        // Die Zutaten fallen über den Fremdschlüssel mit.
        await db.Recipes
            .Where(r => r.Id == recipeId)
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // ----------------------------------------------------------------------- Crafting-Baum

    /// <summary>
    /// Löst auf, woraus ein Item hergestellt wird — und woraus dessen Zutaten wiederum
    /// hergestellt werden. Der gesamte Rezeptbestand eines Projekts wird einmal geladen und
    /// dann im Speicher aufgelöst; das ist bei der Größenordnung eines Spielprojekts deutlich
    /// billiger als eine Abfrage je Ebene.
    /// </summary>
    public async Task<CraftingTreeNode?> BuildTreeAsync(
        Guid projectId, Guid itemId, CancellationToken ct = default)
    {
        var graph = await LoadGraphAsync(projectId, ct);

        return graph.Items.ContainsKey(itemId)
            ? graph.Build(itemId, quantity: 1, path: [], depth: 0)
            : null;
    }

    /// <summary>
    /// Rechnet einen Baum auf seine Grundstoffe herunter: was man tatsächlich sammeln muss,
    /// um die Wurzel einmal herzustellen.
    /// <para>
    /// Rezeptausbeuten werden dabei verrechnet. Ein Rezept, das vier Stäbe auf einmal liefert,
    /// muss für sechs Stäbe zweimal ausgeführt werden — deshalb wird je Stufe aufgerundet.
    /// Der Rest bleibt übrig; ein Lagerbestand wird nicht mitgeführt.
    /// </para>
    /// </summary>
    public static List<CraftingRequirement> SummarizeBaseCost(CraftingTreeNode root)
    {
        var totals = new Dictionary<Guid, CraftingRequirement>();
        Accumulate(root, root.Quantity, totals);

        // Ohne Rezept ist die Wurzel selbst der Grundstoff — dann gibt es nichts zu sammeln.
        return root.Children.Count == 0
            ? []
            : [.. totals.Values.OrderByDescending(entry => entry.Quantity).ThenBy(entry => entry.Name)];
    }

    private static void Accumulate(
        CraftingTreeNode node, int needed, Dictionary<Guid, CraftingRequirement> totals)
    {
        if (node.Children.Count == 0)
        {
            totals[node.ItemId] = totals.TryGetValue(node.ItemId, out var existing)
                ? existing with { Quantity = existing.Quantity + needed }
                : new CraftingRequirement(node.ItemId, node.ItemName, needed, node.PrimaryAssetId);

            return;
        }

        // Wie oft das Rezept laufen muss, um die benötigte Menge zu decken.
        var crafts = (int)Math.Ceiling(needed / (double)Math.Max(1, node.RecipeOutputQuantity));

        foreach (var child in node.Children)
        {
            Accumulate(child, crafts * child.Quantity, totals);
        }
    }

    /// <summary>
    /// Alle Items, die in einem Zyklus stehen — das Konzept führt „zyklische Rezepte“ als
    /// Health Check. Ein Zyklus heißt: das Item wird mittelbar aus sich selbst hergestellt.
    /// </summary>
    public async Task<List<CraftingCycle>> FindCyclesAsync(Guid projectId, CancellationToken ct = default)
    {
        var graph = await LoadGraphAsync(projectId, ct);
        return graph.FindCycles();
    }

    private async Task<CraftingGraph> LoadGraphAsync(Guid projectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var items = await db.Items
            .AsNoTracking()
            .Where(i => i.GameProjectId == projectId)
            .Select(i => new
            {
                i.Id,
                i.Name,
                AssetId = db.Assets
                    .Where(a => a.OwnerEntityId == i.Id && a.IsPrimary)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var recipes = await db.Recipes
            .AsNoTracking()
            .Where(r => r.GameProjectId == projectId && r.OutputItemId != null)
            .Include(r => r.Ingredients)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return new CraftingGraph(
            items.ToDictionary(i => i.Id, i => (i.Name, i.AssetId)),
            recipes,
            messages["DeletedItem"],
            messages["Deleted"]);
    }

    /// <summary>
    /// Der Rezeptbestand eines Projekts im Speicher, aufbereitet zum Auflösen von Bäumen.
    /// </summary>
    private sealed class CraftingGraph(
        Dictionary<Guid, (string Name, Guid? AssetId)> items,
        List<Recipe> recipes,
        string deletedItemName,
        string deletedName)
    {
        public Dictionary<Guid, (string Name, Guid? AssetId)> Items { get; } = items;

        /// <summary>Platzhalter für Items, die es nicht mehr gibt — aus der resx hereingereicht,
        /// weil die Klasse selbst keinen Localizer bekommt.</summary>
        private string DeletedItemName { get; } = deletedItemName;

        private string DeletedName { get; } = deletedName;

        /// <summary>Rezepte je hergestelltem Item — mehrere Wege zum selben Item sind erlaubt.</summary>
        private readonly Dictionary<Guid, List<Recipe>> _byOutput = recipes
            .GroupBy(r => r.OutputItemId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        public CraftingTreeNode Build(Guid itemId, int quantity, HashSet<Guid> path, int depth)
        {
            var (name, assetId) = Items.GetValueOrDefault(itemId, (DeletedItemName, null));

            // Steht das Item schon im Pfad, würde weiteres Auflösen endlos laufen.
            if (path.Contains(itemId))
            {
                return new CraftingTreeNode(
                    itemId, name, assetId, quantity, null, null, 1, 0, IsCycle: true, []);
            }

            var candidates = _byOutput.GetValueOrDefault(itemId, []);
            var recipe = candidates.FirstOrDefault();

            if (recipe is null || depth >= MaximumTreeDepth)
            {
                return new CraftingTreeNode(
                    itemId, name, assetId, quantity, null, null, 1, candidates.Count, IsCycle: false, []);
            }

            var nested = new HashSet<Guid>(path) { itemId };

            var children = recipe.Ingredients
                .OrderBy(ingredient => ingredient.SortOrder)
                .Select(ingredient => Build(ingredient.ItemId, ingredient.Quantity, nested, depth + 1))
                .ToList();

            return new CraftingTreeNode(
                itemId,
                name,
                assetId,
                quantity,
                recipe.Id,
                recipe.Name,
                recipe.OutputQuantity,
                candidates.Count - 1,
                IsCycle: false,
                children);
        }

        /// <summary>
        /// Sucht Items, die mittelbar aus sich selbst hergestellt werden. Tiefensuche mit
        /// Markierung des aktuellen Pfads — der klassische Weg, Zyklen in einem gerichteten
        /// Graphen zu finden.
        /// </summary>
        public List<CraftingCycle> FindCycles()
        {
            var cycles = new List<CraftingCycle>();
            var settled = new HashSet<Guid>();
            var reported = new HashSet<string>();

            foreach (var itemId in _byOutput.Keys)
            {
                Walk(itemId, [], settled, cycles, reported);
            }

            return cycles;
        }

        private void Walk(
            Guid itemId,
            List<Guid> path,
            HashSet<Guid> settled,
            List<CraftingCycle> cycles,
            HashSet<string> reported)
        {
            var position = path.IndexOf(itemId);
            if (position >= 0)
            {
                var cycle = path[position..];

                // Derselbe Zyklus wird von jedem seiner Items aus gefunden — die kleinste
                // GUID zuerst zu drehen macht die Schreibweise eindeutig.
                var rotation = cycle.IndexOf(cycle.Min());
                var normalized = cycle[rotation..].Concat(cycle[..rotation]).ToList();

                if (reported.Add(string.Join(">", normalized)))
                {
                    cycles.Add(new CraftingCycle(
                        [.. normalized.Select(id => Items.GetValueOrDefault(id, (DeletedName, null)).Name)]));
                }

                return;
            }

            if (!settled.Add(itemId))
            {
                return;
            }

            path.Add(itemId);

            foreach (var recipe in _byOutput.GetValueOrDefault(itemId, []))
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    Walk(ingredient.ItemId, path, settled, cycles, reported);
                }
            }

            path.RemoveAt(path.Count - 1);
        }
    }
}

/// <summary>Ein gefundener Rezept-Zyklus, als Kette von Item-Namen.</summary>
public sealed record CraftingCycle(IReadOnlyList<string> ItemNames)
{
    public override string ToString() => string.Join(" → ", ItemNames.Append(ItemNames[0]));
}
