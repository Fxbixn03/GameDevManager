using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Web.Components;
using GameDevManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Die neutralen .resx-Dateien liegen direkt neben ihrer Seite (kein ResourcesPath) und
// tragen die deutschen Texte; weitere Sprachen kommen später als Satelliten-Dateien dazu.
builder.Services.AddLocalization();
builder.Services.AddMudServices();
builder.Services.AddGameDevManagerDatabase(builder.Configuration);
builder.Services.AddGameDevManagerAssetStorage(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScoped<ProjectContext>();

// Beschriftungen, die aus C# statt aus einer Razor-Datei kommen (Modulnamen, Feldtypen,
// Bedingungen). Sie sind Dienste und keine statischen Klassen mehr, weil sie einen
// IStringLocalizer brauchen.
builder.Services.AddSingleton<ModuleLabels>();
builder.Services.AddSingleton<ConditionLabels>();
builder.Services.AddSingleton<FieldTypeLabels>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Liefert eine hochgeladene Datei aus. Bewusst ein Endpunkt und kein statisches Verzeichnis:
// so bleibt der Speicherpfad frei wählbar und lässt sich später hinter eine Anmeldung legen.
app.MapGet("/assets/{id:guid}", async (Guid id, AssetService assets, HttpContext http, CancellationToken ct) =>
{
    var file = await assets.OpenContentAsync(id, ct);
    if (file is null)
    {
        return Results.NotFound();
    }

    // Der Inhalt eines Assets ändert sich nie — eine neue Datei bekommt eine neue GUID.
    http.Response.Headers.CacheControl = "private, max-age=604800, immutable";

    // Hier werden vom Nutzer hochgeladene Dateien zurückgegeben. SVG darf Skripte enthalten,
    // deshalb wird die Auslieferung so eng wie möglich gehalten.
    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

    return Results.Stream(file.Value.Content, file.Value.MimeType);
});

// Ausstehende Migrationen beim Start anwenden (abschaltbar über "Database:AutoMigrate": false).
var dbOptions = app.Services.GetRequiredService<DatabaseOptions>();
var contextFactory = app.Services.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>();

if (dbOptions.AutoMigrate)
{
    await using var db = await contextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    await ProjectContext.EnsureDefaultProjectAsync(contextFactory);
}

app.Run();
