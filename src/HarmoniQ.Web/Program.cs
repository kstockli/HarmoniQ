using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using HarmoniQ.Web.Components;
using HarmoniQ.Web.Components.Account;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddMudServices();

builder.Services.AddHttpClient<YouTubeMetadataService>();
builder.Services.AddHttpClient<YouTubeSearchService>();
builder.Services.AddHttpClient<WebseitenScraper>(c =>
{
    // Manche Webseiten blocken Requests ohne User-Agent.
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MusicRaterBot/1.0)");
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Google-Login nur aktivieren, wenn Credentials konfiguriert sind (z. B. via user-secrets).
// Ohne Konfiguration startet die App normal – nur ohne den Google-Button.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Factory für interaktive Blazor-Komponenten (thread-sicher: pro Operation ein eigener Kontext).
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
// Zusätzlich ein scoped Kontext aus der Factory – wird von ASP.NET Core Identity benötigt.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Für die Testphase: kein E-Mail-Bestätigungs-Zwang, damit man sich sofort
        // einloggen kann (auch via Google, das die Mail-Adresse ohnehin verifiziert).
        // Für Produktion ggf. wieder auf true setzen – E-Mail-Versand ist konfiguriert.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();

// Befördert konfigurierte Admin-Mails sofort beim Login (ohne Neustart) zum Admin.
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AdminClaimsTransformation>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
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

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await AdminInitializer.EnsureAdminsAsync(scope.ServiceProvider, app.Configuration, logger);
    // Einmalige Roster-Importe (Stadtmusik/JBL Luzern) sind erfolgt und persistiert.
    // Mitglieder werden künftig über den Admin-Editor (/admin/bands/{id}/mitglieder) gepflegt.
    // Die Importer-Klassen bleiben als Provenienz-/Re-Import-Quelle erhalten.
}

app.Run();
