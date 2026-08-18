using System.Globalization;
using GameDevManager.Data;
using GameDevManager.Data.Services;
using GameDevManager.Domain.Entities;
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
builder.Services.AddSingleton<LanguageSelection>();
builder.Services.AddSingleton<TopbarSelection>();
builder.Services.AddSingleton<PasswordPolicySelection>();

// Beschriftungen, die aus C# statt aus einer Razor-Datei kommen (Modulnamen, Feldtypen,
// Bedingungen). Sie sind Dienste und keine statischen Klassen mehr, weil sie einen
// IStringLocalizer brauchen.
builder.Services.AddSingleton<ModuleLabels>();
builder.Services.AddSingleton<ConditionLabels>();
builder.Services.AddSingleton<FieldTypeLabels>();
builder.Services.AddSingleton<ChangeActionLabels>();
builder.Services.AddSingleton<ContentStatusLabels>();
builder.Services.AddSingleton<AccountLabels>();
builder.Services.AddSingleton<ISystemUserName>(sp => sp.GetRequiredService<AccountLabels>());

// Anmeldung über ein Cookie. Bewusst kein ASP.NET-Identity: Gebraucht wird ein Konto mit
// Passwort, und dafür sieben Identity-Tabellen in alle vier Provider zu migrieren wäre ein
// Vielfaches an Umfang für dasselbe Ergebnis — das Hashing steht in PasswordHasher.
// Der externe Anbieter ist Installations-Konfiguration; ohne Authority und ClientId bleibt er
// abgeschaltet, und die Anmeldeseite zeigt den Knopf gar nicht erst.
var externalLogin = builder.Configuration
    .GetSection(ExternalLoginOptions.SectionName)
    .Get<ExternalLoginOptions>() ?? new ExternalLoginOptions();

builder.Services.AddSingleton(externalLogin);

var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

if (externalLogin.IsConfigured)
{
    // OpenID Connect als **zweites** Schema neben dem Cookie: Die Anmeldung selbst bleibt das
    // Cookie — der externe Anbieter beweist nur, wer da ist, und der Rest (Ansprüche,
    // Berechtigungen, IChangeAuthorProvider) läuft unverändert weiter.
    authentication.AddOpenIdConnect(ExternalLoginDefaults.Scheme, externalLogin.DisplayName, options =>
    {
        options.Authority = externalLogin.Authority;
        options.ClientId = externalLogin.ClientId;
        options.ClientSecret = externalLogin.ClientSecret;

        // Der Authorization-Code-Ablauf: Das Token geht über den Server und nie durch den
        // Browser des Nutzers.
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;

        options.CallbackPath = "/konto/extern/rueckkehr";
        options.SignedOutCallbackPath = "/konto/extern/abgemeldet";

        // Das Ergebnis landet in einem eigenen Zwischen-Cookie; das Anmelde-Cookie setzt erst
        // die Rückkehr-Seite, nachdem sie das Konto zum externen Bezeichner gefunden hat.
        options.SignInScheme = ExternalLoginDefaults.TemporaryScheme;

        foreach (var scope in externalLogin.Scopes)
        {
            options.Scope.Add(scope);
        }
    })
    // Ein eigenes Zwischen-Cookie für den halben Weg: Ohne es wäre die externe Anmeldung
    // schon die Anmeldung — auch für einen, der hier gar kein Konto hat.
    .AddCookie(ExternalLoginDefaults.TemporaryScheme, options =>
    {
        options.Cookie.Name = "gdm.external";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });
}

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

// Dasselbe Muster für den Mailversand: Die Datenschicht kennt nur IMailSender (Vorgabe
// No-Op), verschickt wird hier — und ohne „Mail:Host“ bleibt auch diese Fassung still.
builder.Services.Replace(ServiceDescriptor.Singleton<IMailSender, SmtpMailSender>());

// Kürzt das Änderungsprotokoll auf die eingestellte Aufbewahrung. Läuft nach dem Start —
// Hintergrunddienste beginnen erst mit app.Run(), also nach den Migrationen weiter unten.
builder.Services.AddHostedService<ChangeLogMaintenance>();

// Legt zur eingestellten Uhrzeit je Projekt einen Exportstand an — nur, wenn sich seit dem
// letzten Stand etwas geändert hat. Ohne „Exports:ScheduleTime“ tut er nichts.
builder.Services.AddHostedService<ScheduledExportSnapshots>();

// Ruft die Webhooks eines Projekts auf, wenn sich etwas geändert hat. Der Zeitgeber bündelt
// die Änderungen einer Bearbeitungssitzung zu einem Aufruf; die Zeitgrenze ist knapp, weil ein
// Empfänger, der nicht antwortet, den Dienst nicht aufhalten darf.
builder.Services.AddHttpClient(nameof(WebhookDispatcher), client =>
    client.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddHostedService<WebhookDispatcher>();

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

    // Range-Anfragen werden beantwortet (206 samt Accept-Ranges/Content-Range): Der
    // Audio-Player des Browsers spult in langen Aufnahmen über genau solche Anfragen.
    // Die Dateien kommen als FileStream und sind damit spulbar; am Cache-Verhalten und
    // an nosniff/CSP ändert sich nichts.
    return Results.Stream(file.Value.Content, file.Value.MimeType, enableRangeProcessing: true);
}).RequireAuthorization();

// Der Projekt-Export als ZIP-Download. Wie die Assets bewusst ein Endpunkt: über die
// SignalR-Verbindung von Blazor Server lässt sich keine Datei ausliefern, der Browser
// lädt hier direkt herunter. Die Export-Seite baut nur die URL auf diesen Endpunkt.
app.MapGet("/export/{projectId:guid}", async (
    Guid projectId, string? target, bool? assets, string? minimumStatus, string? moduleKeys,
    string? layout,
    ExportService export,
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

    // Ohne Angabe geht alles hinaus — der Filter ist eine Einschränkung, keine Vorgabe.
    ContentStatus? floor = Enum.TryParse<ContentStatus>(minimumStatus, ignoreCase: true, out var status)
        ? status
        : null;

    // Ohne Angabe gehen alle Module hinaus — dieselbe Regel wie beim Statusfilter.
    var modules = UserPermissions.ParseModuleKeys(moduleKeys);

    // Ohne Angabe eine Datei je Modul — das Ordner-Layout ist für Git und die Ausnahme.
    var exportLayout = Enum.TryParse<ExportLayout>(layout, ignoreCase: true, out var chosenLayout)
        ? chosenLayout
        : ExportLayout.SingleFile;

    // Der Projektname wird Teil des Dateinamens — unzulässige Zeichen fliegen raus.
    var safeName = string.Join("-", project.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    var fileName = $"{safeName}-{exportTarget.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    return Results.Stream(
        stream => export.WriteExportAsync(
            projectId, exportTarget, assets ?? true, stream, floor, modules, exportLayout, ct),
        "application/zip",
        fileDownloadName: fileName);
}).RequireAuthorization();

// Die Sicherung der ganzen Installation: alle Projekte plus das, was in keinem
// Projekt-Export steht — Benutzer, Rollen, API-Schlüssel, Boards, Protokoll. Wie der
// Projekt-Export ein Endpunkt, weil sich über SignalR keine Datei ausliefern lässt.
app.MapGet("/export/installation", (
    InstallationBackupService backup, HttpContext http, CancellationToken ct) =>
{
    // Eine Installations-Sicherung enthält den gesamten Bestand samt Konten — sie verlangt
    // beides: das Exportrecht und den Verwalter.
    if (!http.User.Permissions().CanExport || !http.User.Permissions().IsAdministrator)
    {
        return Results.Forbid();
    }

    var fileName = $"gamedevmanager-installation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    return Results.Stream(
        stream => backup.WriteBackupAsync(stream, ct), "application/zip", fileDownloadName: fileName);
}).RequireAuthorization();

// ---------------------------------------------------------------- Lesende HTTP-API (v1)
//
// Die Vorstufe zu einem Editor-Plugin: Inhalte je Projekt als JSON, mit denselben
// Serialisierungsregeln wie der Export — wer das ZIP lesen kann, kann auch das hier lesen.
//
// Bewusst **nur lesend**: Ein Schlüssel, der schreiben dürfte, wäre ein zweiter Weg an
// Rechteprüfung, Änderungsprotokoll und Schreibkonflikt-Erkennung vorbei.
//
// Die Anmeldung läuft über den Header „X-API-Key“ (alternativ „Authorization: Bearer …“) und
// nicht über das Cookie: Ein Plugin hat keinen Browser, in dem eines läge.
// ------------------------------------------------------------------- Betriebs-Kennzahlen
//
// „/health“ beantwortet die eine Frage, die eine Überwachung ohne Zugang stellen darf: Läuft
// die Anwendung und antwortet ihre Datenbank? Mehr steht dort nicht — schon die Zahl der
// Projekte verriete die Größe des Bestands.
app.MapGet("/health", async (OperationsMetricsService metrics, CancellationToken ct) =>
{
    var (reachable, _) = await metrics.CheckDatabaseAsync(ct);

    return reachable
        ? Results.Text("healthy", "text/plain")
        : Results.Text("unhealthy", "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

var api = app.MapGroup("/api/v1");

api.AddEndpointFilter(async (context, next) =>
{
    var http = context.HttpContext;
    var keys = http.RequestServices.GetRequiredService<ApiKeyService>();

    var presented = http.Request.Headers["X-API-Key"].FirstOrDefault()
        ?? (http.Request.Headers.Authorization.FirstOrDefault() is { } header
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header["Bearer ".Length..]
                : null);

    var key = await keys.ValidateAsync(presented, http.RequestAborted);
    if (key is null)
    {
        return Results.Unauthorized();
    }

    // Ein auf ein Projekt beschränkter Schlüssel sieht nur dieses — geprüft an einer Stelle
    // statt in jedem Endpunkt.
    if (key.GameProjectId is { } allowed
        && http.Request.RouteValues.TryGetValue("projectId", out var requested)
        && Guid.TryParse(requested?.ToString(), out var projectId)
        && projectId != allowed)
    {
        return Results.Forbid();
    }

    http.Items["ApiKey"] = key;
    return await next(context);
});

api.MapGet("/projects", async (ContentApiService content, HttpContext http, CancellationToken ct) =>
{
    var key = (ApiKey)http.Items["ApiKey"]!;
    return Results.Json(await content.GetProjectsAsync(key.GameProjectId, ct), ContentApiService.JsonOptions);
});

// Die Zahlen dagegen hinter dem Schlüssel — der Filter der Gruppe steht ohnehin schon da.
api.MapGet("/metrics", async (OperationsMetricsService metrics, HttpContext http, CancellationToken ct) =>
{
    var collected = await metrics.CollectAsync(ct);

    // Prometheus, wenn es danach fragt; sonst dasselbe als JSON, weil ein Blick per Browser
    // der häufigere Fall ist.
    var wantsText = http.Request.Query["format"] == "prometheus"
        || http.Request.Headers.Accept.Any(value => value?.Contains("text/plain") == true);

    return wantsText
        ? Results.Text(OperationsMetricsService.ToPrometheus(collected), "text/plain; version=0.0.4")
        : Results.Json(collected, ContentApiService.JsonOptions);
});

api.MapGet("/modules", (ContentApiService content) =>
    Results.Json(new { modules = content.ModuleKeys }, ContentApiService.JsonOptions));

api.MapGet("/projects/{projectId:guid}/modules/{moduleKey}", async (
    Guid projectId, string moduleKey, string? language, ContentApiService content, CancellationToken ct) =>
{
    var payload = await content.GetModuleAsync(projectId, moduleKey, language, ct);

    return payload is null
        ? Results.NotFound()
        : Results.Json(payload, ContentApiService.JsonOptions);
});

api.MapGet("/projects/{projectId:guid}/modules/{moduleKey}/{entityId:guid}", async (
    Guid projectId, string moduleKey, Guid entityId, ContentApiService content, CancellationToken ct) =>
{
    var payload = await content.GetEntityAsync(projectId, moduleKey, entityId, ct);

    return payload is null
        ? Results.NotFound()
        : Results.Json(payload, ContentApiService.JsonOptions);
});

// Der schreibende Teil (F36). Bewusst kein Löschen: Das räumt Assets, Kind-Sammlungen und
// Bedingungen ab — je Modul anders, und ein generischer Löschpfad wäre die Stelle, an der ein
// Modul etwas liegen ließe. Wer löschen will, tut es in der Oberfläche.
api.MapPost("/projects/{projectId:guid}/modules/{moduleKey}", async (
    Guid projectId, string moduleKey, ContentWrite write,
    ContentApiWriteService writer, IUserPermissionsProvider permissions,
    HttpContext http, CancellationToken ct) =>
{
    var key = (ApiKey)http.Items["ApiKey"]!;

    // Ein Schlüssel ist zuerst ein Lesezugang; das Schreibrecht wird ausdrücklich vergeben —
    // und ohne Konto gibt es keins, weil das Änderungsprotokoll einen Urheber braucht.
    if (!key.CanWriteNow)
    {
        return Results.Forbid();
    }

    // Der If-Match-Kopfeintrag trägt den Stand, von dem der Aufrufer ausgeht; die
    // Schreibkonflikt-Erkennung in StageValuesAsync prüft ihn wie bei jeder Maske.
    if (http.Request.Headers.IfMatch.FirstOrDefault()?.Trim('"') is { } tag
        && DateTime.TryParse(tag, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var expected))
    {
        write.ExpectedUpdatedAtUtc = expected;
    }

    try
    {
        var result = await writer.WriteAsync(
            projectId, moduleKey, write,
            http.Request.Headers["Idempotency-Key"].FirstOrDefault(), ct);

        http.Response.Headers.ETag = $"\"{result.UpdatedAtUtc:o}\"";

        return result.Created
            ? Results.Created($"/api/v1/projects/{projectId}/modules/{moduleKey}/{result.Id}", result)
            : Results.Json(result, ContentApiService.JsonOptions);
    }
    catch (ContentConcurrencyException ex)
    {
        // 409 und nicht 400: Der Aufruf war richtig, nur der Stand ist überholt.
        return Results.Conflict(new { error = ex.Message });
    }
    catch (ContentValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Der CSV-Export eines einzelnen Moduls — der Weg Tabelle ↔ Tool fürs Balancing. Wie beim
// ZIP über einen Endpunkt und nicht über den Blazor-Kreis: Über SignalR lässt sich kein
// Download anstoßen.
// Die offenen Übersetzungen einer Sprache als Tabelle — der Weg zu einem externen
// Übersetzer, der keinen Zugang zum Tool braucht. Wie beim Modul-CSV ein Endpunkt.
app.MapGet("/export/translations/{projectId:guid}/{languageCode}", async (
    Guid projectId, string languageCode, bool? openOnly, LocalizationService localization,
    HttpContext http, CancellationToken ct) =>
{
    if (!http.User.Permissions().CanExport)
    {
        return Results.Forbid();
    }

    var content = await localization.ExportCsvAsync(projectId, languageCode, openOnly ?? true, ct);

    // Mit BOM, aus demselben Grund wie beim Modul-CSV.
    byte[] bytes = [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes(content)];

    return Results.File(
        bytes,
        "text/csv; charset=utf-8",
        fileDownloadName: $"uebersetzungen-{languageCode}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
}).RequireAuthorization();

app.MapGet("/export/csv/{projectId:guid}/{moduleKey}", async (
    Guid projectId, string moduleKey, CsvContentService csv, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.Permissions().CanExport || !http.User.Permissions().CanAccessModule(moduleKey))
    {
        return Results.Forbid();
    }

    var content = await csv.ExportAsync(projectId, moduleKey, ct);

    // Mit BOM: Sonst zeigt Excel Umlaute aus einer UTF-8-Datei als Buchstabensalat, und der
    // CSV-Leser überspringt es beim Wiedereinlesen ohnehin.
    byte[] bytes = [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes(content)];

    return Results.File(
        bytes,
        "text/csv; charset=utf-8",
        fileDownloadName: $"{moduleKey}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
}).RequireAuthorization();

// Das Design-Dokument: der Bestand als eigenständige, druckbare HTML-Datei (F40).
// ?chapters=story,npcs schränkt die Kapitel ein; ohne Angabe kommt alles.
app.MapGet("/export/design/{projectId:guid}", async (
    Guid projectId, string? chapters, DesignDocumentService design, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.Permissions().CanExport)
    {
        return Results.Forbid();
    }

    var wanted = chapters?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.ToLowerInvariant())
        .ToHashSet();

    var selection = wanted is null
        ? DesignChapters.All
        : new DesignChapters(
            wanted.Contains("story"),
            wanted.Contains("factions"),
            wanted.Contains("npcs"),
            wanted.Contains("quests"),
            wanted.Contains("items"));

    var html = await design.BuildHtmlAsync(projectId, selection, ct);

    return Results.File(
        System.Text.Encoding.UTF8.GetBytes(html),
        "text/html; charset=utf-8",
        fileDownloadName: $"design-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html");
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
// Die Sprache der Oberfläche gilt für alles, was danach gerendert wird — gesetzt einmal beim
// Start, geändert über die Einstellungen. Blazor Server rendert serverseitig, also ist die
// Kultur des Prozesses das, was der IStringLocalizer liest.
app.Services.GetRequiredService<LanguageSelection>().Apply();

if (app.Services.GetRequiredService<DatabaseOptions>().AutoMigrate)
{
    using var scope = app.Services.CreateScope();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GameDevManagerDbContext>>();

    await using var db = await contextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    await ProjectContext.EnsureDefaultProjectAsync(contextFactory);
}

app.Run();
