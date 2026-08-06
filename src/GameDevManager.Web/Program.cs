using GameDevManager.Data;
using GameDevManager.Web.Components;
using GameDevManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddGameDevManagerDatabase(builder.Configuration);
builder.Services.AddScoped<ProjectContext>();

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
