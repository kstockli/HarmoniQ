using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

// Factory für interaktive Blazor-Komponenten (thread-sicher: pro Operation ein eigener Kontext).
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
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
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    // Railway hat mehrere Proxy-Hops → kein Limit, damit das ursprüngliche https/Host gilt.
    options.ForwardLimit = null;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// In Produktion gibt der Host (Railway) den Port via Umgebungsvariable PORT vor.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Reverse-Proxy-Header zuerst auswerten (vor Auth/HTTPS-Redirect).
app.UseForwardedHeaders();

// TEMP-Diagnose: zeigt beim OAuth-Callback, welches Scheme/Host die App effektiv sieht
// (daraus baut die OAuth-Middleware die redirect_uri für den Token-Tausch).
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    if (p.StartsWith("/signin-google") || p.StartsWith("/signin-microsoft"))
    {
        var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OAuthDiag");
        log.LogWarning("OAuth-Callback {Path}: Scheme={Scheme} Host={Host} X-Forwarded-Proto='{XFP}' X-Forwarded-Host='{XFH}'",
            p, ctx.Request.Scheme, ctx.Request.Host.Value,
            ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
            ctx.Request.Headers["X-Forwarded-Host"].ToString());
    }
    await next();
});

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
    // Roster-Importe (Stadtmusik/JBL Luzern) sind erfolgt und in der Postgres-DB persistiert.
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
