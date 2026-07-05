using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using HarmoniQ.Web.Components;
using HarmoniQ.Web.Components.Account;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Services;
using HarmoniQ.Web.Services.Crawler;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddMudServices();

// In-Memory-Cache für globale, nicht user-spezifische Startseiten-Daten (Zähler, Featured-
// Komponist:innen/Bands) – nimmt ~8 DB-Queries pro Seitenaufbau aus dem Hot-Path (siehe Home.razor).
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<YouTubeMetadataService>();
builder.Services.AddHttpClient<YouTubeSearchService>();
builder.Services.AddHttpClient<WebseitenScraper>(c =>
{
    // Manche Webseiten blocken Requests ohne User-Agent.
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MusicRaterBot/1.0)");
    c.Timeout = TimeSpan.FromSeconds(20);
});

// Crawler / Import-Roboter (Spezifikation-Crawler.md). Optionen aus appsettings „Crawler“;
// Fetch-Stufe (HTML + PDF, robots.txt, Rate-Limit) als typisierter HttpClient.
builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection(CrawlerOptions.Section));
// JS-Rendering (Playwright) als Singleton – Browser wird lazy gestartet, bei Nichtverfügbarkeit
// fällt der Fetch automatisch auf HTTP zurück. Nur wirksam bei Crawler:RenderingAktiv=true.
builder.Services.AddSingleton<ISeitenRenderer, PlaywrightRenderer>();
builder.Services.AddHttpClient<CrawlFetchService>();
// Wikipedia-Anreicherung (kein Key; Wikipedia verlangt einen erkennbaren User-Agent).
builder.Services.AddHttpClient<WikipediaService>(c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        builder.Configuration["Crawler:UserAgent"] ?? "HarmoniQBot/1.0 (+https://harmoniq.q-no.ch)");
    c.Timeout = TimeSpan.FromSeconds(20);
});
// Adress-Geocoding via Nominatim/OSM (Lokal-CRUD). Nominatim verlangt einen echten User-Agent.
builder.Services.AddHttpClient<GeocodingService>(c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd(
        builder.Configuration["Crawler:UserAgent"] ?? "HarmoniQBot/1.0 (+https://harmoniq.q-no.ch)");
    c.Timeout = TimeSpan.FromSeconds(15);
});
// Extraktor: Mistral „La Plateforme", wenn konfiguriert (Crawler:Llm:Provider=mistral + ApiKey);
// sonst Stub (manuelle Erfassung im Review).
var llmProvider = builder.Configuration["Crawler:Llm:Provider"];
var llmKey = builder.Configuration["Crawler:Llm:ApiKey"];
if (string.Equals(llmProvider, "mistral", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(llmKey))
    builder.Services.AddHttpClient<IExtraktion, MistralExtraktion>(c =>
        c.Timeout = TimeSpan.FromSeconds(240));
else
    builder.Services.AddScoped<IExtraktion, StubExtraktion>();

// Komponist-Suche (Web + LLM) für fehlende Selbstwahlstück-Komponist:innen (SBBW §4.2).
builder.Services.AddHttpClient<KomponistSuche>(c => c.Timeout = TimeSpan.FromSeconds(20));

// Einmaliger WMC-2026-Import (Admin-Seite /admin/wmc-import, Dry-run → Übernehmen).
builder.Services.AddHttpClient<HarmoniQ.Web.Services.Wmc.WmcImportService>(c => c.Timeout = TimeSpan.FromSeconds(40));

// Orchestrator: In-Memory-Queue (Singleton) + Hintergrund-Dienst, der Läufe sequenziell abarbeitet.
builder.Services.AddSingleton<CrawlLaufQueue>();
builder.Services.AddScoped<CrawlRunner>();
builder.Services.AddHostedService<CrawlHostedService>();
builder.Services.AddHostedService<WochenBenachrichtigungHostedService>();

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

// Microsoft-Login nur aktivieren, wenn Credentials konfiguriert sind (Azure-App-Registrierung).
var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrWhiteSpace(microsoftClientId) && !string.IsNullOrWhiteSpace(microsoftClientSecret))
{
    builder.Services.AddAuthentication().AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

// Connection-String: lokal aus ConnectionStrings:DefaultConnection (user-secrets);
// in Produktion (Railway) liefert die Postgres-Plugin-Variable DATABASE_URL einen URL,
// den wir in das Npgsql-Format umwandeln.
var connectionString = AufloesenConnectionString(builder.Configuration);

// Zirkuit-gebundener Benutzer-Kontext für die Audit-Spalten (createuser/modifyuser).
builder.Services.AddScoped<HarmoniQ.Web.Data.BenutzerKontext>();
// Factory für interaktive Blazor-Komponenten (thread-sicher: pro Operation ein eigener Kontext).
// Scoped, damit der erzeugte Kontext den circuit-scoped BenutzerKontext injiziert bekommt.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString), ServiceLifetime.Scoped);
// Zusätzlich ein scoped Kontext aus der Factory – wird von ASP.NET Core Identity benötigt.
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Login erst nach E-Mail-Bestätigung. Externe Logins (Google/Microsoft) bestätigen
        // das Konto automatisch (Provider verifiziert die Mail) – siehe ExternalLogin.razor.
        // Bestehende Konten wurden per Migration einmalig bestätigt (Aussperr-Schutz).
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddErrorDescriber<DeutscherIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// E-Mail-Versand: per HTTPS-API (Resend) wenn ein API-Key konfiguriert ist (z. B. Prod auf
// Railway, wo SMTP geblockt ist), sonst über SMTP (lokal/MailKit).
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:Resend:ApiKey"]))
    builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, ResendEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();
// Derselbe Sender dient auch für App-Benachrichtigungen (z. B. Freundschaftsanfragen).
builder.Services.AddScoped<IBenachrichtigungsMail>(sp =>
    (IBenachrichtigungsMail)sp.GetRequiredService<IEmailSender<ApplicationUser>>());

// Befördert konfigurierte Admin-Mails sofort beim Login (ohne Neustart) zum Admin.
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AdminClaimsTransformation>();

// DataProtection-Schlüssel in der DB ablegen → Login-Cookies/Tokens überleben Neustart & Redeploy.
builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();

// Hinter einem Reverse-Proxy (Railway) das X-Forwarded-Proto/-For übernehmen, damit OAuth-
// Redirects korrekt auf https zeigen.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    // Railway hat mehrere Proxy-Hops → kein Limit, damit das ursprüngliche https gilt.
    options.ForwardLimit = null;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// In Produktion gibt der Host (Railway) den Port via Umgebungsvariable PORT vor.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Reverse-Proxy-Header auswerten (vor Auth/HTTPS-Redirect).
app.UseForwardedHeaders();

// Hinter Railway terminiert der Edge-Proxy TLS und spricht intern http mit dem Container.
// In Produktion erzwingen wir daher das Scheme https, damit die OAuth-Middleware die
// redirect_uri (Authorize & Token-Tausch) korrekt mit https baut.
if (!app.Environment.IsDevelopment())
{
    app.Use((ctx, next) =>
    {
        ctx.Request.Scheme = "https";
        return next();
    });
}

// Authentifizierung EXPLIZIT hier einhängen – nach ForwardedHeaders/Scheme-Fix, damit die
// OAuth-Middleware die redirect_uri mit dem korrekten https-Scheme baut (sonst hängt der
// Framework die Auth automatisch zu früh in die Pipeline → redirect_uri mit http).
app.UseAuthentication();
app.UseAuthorization();

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

// ── Web-Push: Anmeldung eines Geräts speichern/entfernen (PWA-Push, UX-Spec 4.2) ──
app.MapPost("/api/push/subscribe", async (PushAboDto dto,
    System.Security.Claims.ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var uid = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(uid) || string.IsNullOrWhiteSpace(dto.Endpoint)) return Results.BadRequest();

    var vorhanden = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
    if (vorhanden is null)
        db.PushSubscriptions.Add(new HarmoniQ.Web.Data.Models.PushSubscription
        {
            BenutzerId = uid, Endpoint = dto.Endpoint, P256dh = dto.P256dh, Auth = dto.Auth
        });
    else { vorhanden.BenutzerId = uid; vorhanden.P256dh = dto.P256dh; vorhanden.Auth = dto.Auth; }
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

app.MapPost("/api/push/unsubscribe", async (PushAboDto dto, ApplicationDbContext db) =>
{
    var s = await db.PushSubscriptions.FirstOrDefaultAsync(x => x.Endpoint == dto.Endpoint);
    if (s != null) { db.PushSubscriptions.Remove(s); await db.SaveChangesAsync(); }
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogInformation("Crawler JS-Rendering: {Status} (Crawler:RenderingAktiv).",
        app.Configuration.GetValue<bool>("Crawler:RenderingAktiv") ? "AKTIV" : "inaktiv");
    await AdminInitializer.EnsureAdminsAsync(scope.ServiceProvider, app.Configuration, logger);
    // Aktivitäts-Feed einmalig aus Bestandsdaten befüllen (nur wenn leer).
    await AktivitaetBackfill.RunAsync(db);
    // Roster-Importe (Stadtmusik/JBL Luzern) sind lokal und in Prod erfolgt und persistiert.
    // Mitglieder werden künftig über den Admin-Editor gepflegt; Importer-Klassen bleiben als
    // Provenienz-/Re-Import-Quelle erhalten.
}

app.Run();

// ── Helfer ───────────────────────────────────────────────────────────────────

static string AufloesenConnectionString(IConfiguration config)
{
    // Railway & Co. stellen die DB als URL bereit (postgresql://user:pass@host:port/db).
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(url))
        return NpgsqlAusUrl(url);

    return config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Kein Connection-String: weder DATABASE_URL noch ConnectionStrings:DefaultConnection gesetzt.");
}

static string NpgsqlAusUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = Npgsql.SslMode.Require,
        TrustServerCertificate = true
    };
    return builder.ConnectionString;
}

// DTO für die Web-Push-Anmeldung (Client → /api/push/subscribe).
public record PushAboDto(string Endpoint, string P256dh, string Auth);
