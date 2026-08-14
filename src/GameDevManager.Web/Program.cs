using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Web.Components;
using GameDevManager.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Settings changed in the UI (database provider, connection strings) land in this file and
// override appsettings.json on the next start — the checked-in file stays untouched.
builder.Configuration.AddJsonFile(LocalSettingsFile.FileName, optional: true, reloadOnChange: false);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Die neutralen .resx-Dateien liegen direkt neben ihrer Seite (kein ResourcesPath) und
// tragen die deutschen Texte; weitere Sprachen kommen später als Satelliten-Dateien dazu.
builder.Services.AddLocalization();
// Snackbars unten rechts statt in der Standardecke oben rechts — dort verdecken sie
// weder die Appbar noch die globale Suche.
builder.Services.AddMudServices(options =>
    options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight);
builder.Services.AddGameDevManagerDatabase(builder.Configuration);
builder.Services.AddGameDevManagerAssetStorage(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddGameDevManagerExportStorage(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScoped<ProjectContext>();
builder.Services.AddSingleton<ProjectSelection>();
builder.Services.AddScoped<ModuleState>();
builder.Services.AddSingleton<LocalSettingsFile>();
builder.Services.AddSingleton<AppearanceSelection>();
builder.Services.AddSingleton<PasswordPolicySelection>();

// Beschriftungen, die aus C# statt aus einer Razor-Datei kommen (Modulnamen, Feldtypen,
// Bedingungen). Sie sind Dienste und keine statischen Klassen mehr, weil sie einen
// IStringLocalizer brauchen.
builder.Services.AddSingleton<ModuleLabels>();
builder.Services.AddSingleton<ConditionLabels>();
builder.Services.AddSingleton<FieldTypeLabels>();
builder.Services.AddSingleton<ChangeActionLabels>();
builder.Services.AddSingleton<AccountLabels>();
builder.Services.AddSingleton<ISystemUserName>(sp => sp.GetRequiredService<AccountLabels>());

// Anmeldung über ein Cookie. Bewusst kein ASP.NET-Identity: Gebraucht wird ein Konto mit
// Passwort, und dafür sieben Identity-Tabellen in alle vier Provider zu migrieren wäre ein
// Vielfaches an Umfang für dasselbe Ergebnis — das Hashing steht in PasswordHasher.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/konto/anmelden";
        options.LogoutPath = "/konto/abmelden";
        options.AccessDeniedPath = "/konto/anmelden";
        // Derselbe Name, den auch RedirectToLogin anhängt — sonst käme man je nach Weg
        // (Umleitung des Cookies oder des laufenden Kreises) unterschiedlich zurück.
        options.ReturnUrlParameter = "ziel";
        options.Cookie.Name = "gdm.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Das Tool wird self-hosted betrieben, oft ohne HTTPS im lokalen Netz — deshalb
        // „SameAsRequest“ statt „Always“, sonst käme das Cookie über HTTP nie an.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Statisch gerenderte Seiten und die Datei-Endpunkte kennen ihren Benutzer über den
// HttpContext; der laufende Blazor-Kreis hat keinen mehr — siehe BlazorChangeAuthorProvider.
builder.Services.AddHttpContextAccessor();

// Wer gerade handelt, weiß nur die Web-Schicht — die Datenschicht kennt dafür nur die
// Schnittstelle. Ersetzt die Vorgabe „System“ aus AddGameDevManagerContentServices.
builder.Services.Replace(
    ServiceDescriptor.Scoped<IChangeAuthorProvider, BlazorChangeAuthorProvider>());

// Dasselbe Muster für die Berechtigungen: Sie stehen in den Ansprüchen des Cookies, geprüft
// wird in der Datenschicht (WriteGuardInterceptor, PermissionGuard). Ersetzt „alles erlaubt“.
builder.Services.Replace(
    ServiceDescriptor.Scoped<IUserPermissionsProvider, BlazorUserPermissionsProvider>());

// Dasselbe Muster für die Passwortrichtlinie: Konfiguration und Einstellungsseite kennt nur
// die Web-Schicht, der UserService fragt die Schnittstelle.
builder.Services.Replace(ServiceDescriptor.Singleton<IPasswordPolicyProvider>(
    provider => provider.GetRequiredService<PasswordPolicySelection>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // The dev profile binds HTTP only; without a known HTTPS port the redirection would warn on every start.
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
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
}).RequireAuthorization();

// Der Projekt-Export als ZIP-Download. Wie die Assets bewusst ein Endpunkt: über die
// SignalR-Verbindung von Blazor Server lässt sich keine Datei ausliefern, der Browser
// lädt hier direkt herunter. Die Export-Seite baut nur die URL auf diesen Endpunkt.
app.MapGet("/export/{projectId:guid}", async (
    Guid projectId, string? target, bool? assets, ExportService export,
    IDbContextFactory<GameDevManagerDbContext> dbFactory, HttpContext http, CancellationToken ct) =>
{
    // Der Export ist ein eigenes Recht — ohne läuft auch der direkte Aufruf der URL ins Leere.
    if (!http.User.Permissions().CanExport)
    {
        return Results.Forbid();
    }

    await using var db = await dbFactory.CreateDbContextAsync(ct);
    var project = await db.GameProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
    if (project is null)
    {
        return Results.NotFound();
    }

    var exportTarget = Enum.TryParse<ExportTarget>(target, ignoreCase: true, out var parsed)
        ? parsed
        : ExportTarget.Json;

    // Der Projektname wird Teil des Dateinamens — unzulässige Zeichen fliegen raus.
    var safeName = string.Join("-", project.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    var fileName = $"{safeName}-{exportTarget.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    return Results.Stream(
        stream => export.WriteExportAsync(projectId, exportTarget, assets ?? true, stream, ct),
        "application/zip",
        fileDownloadName: fileName);
}).RequireAuthorization();

// Lädt einen aufbewahrten Exportstand herunter. Der Dienst prüft den Dateinamen streng
// (Zeitstempel plus Projekt-GUID) — alles andere ist ein 404, kein Pfad ins Dateisystem.
app.MapGet("/export/snapshots/{fileName}", (string fileName, ExportSnapshotService snapshots, HttpContext http) =>
{
    // Exportstände sind Teil des Exports — dasselbe Recht wie beim Download darüber.
    if (!http.User.Permissions().CanExport)
    {
        return Results.Forbid();
    }

    var stream = snapshots.OpenRead(fileName);
    return stream is null
        ? Results.NotFound()
        : Results.Stream(stream, "application/zip", fileDownloadName: fileName);
}).RequireAuthorization();

// Ausstehende Migrationen beim Start anwenden (abschaltbar über "Database:AutoMigrate": false).
// Über einen eigenen Scope, weil die Context-Factory scoped registriert ist — sie zieht sich
// den ChangeLogInterceptor, und der braucht den angemeldeten Benutzer der Verbindung.
if (app.Services.GetRequiredService<DatabaseOptions>().AutoMigrate)
{
    using var scope = app.Services.CreateScope();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>();

    await using var db = await contextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    await ProjectContext.EnsureDefaultProjectAsync(contextFactory);
}

app.Run();
