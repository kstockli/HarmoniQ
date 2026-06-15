using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using HarmoniQ.Web.Data;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Befördert die in der Konfiguration ("Admin:Emails") hinterlegten Benutzer
/// automatisch in die Rolle "Admin" – und zwar sofort beim (ersten) Login,
/// nicht erst beim nächsten App-Start. Die Rolle wird dauerhaft in der DB
/// gesichert und zusätzlich direkt als Claim am aktuellen Principal ergänzt,
/// damit sie ohne Neustart greift.
/// Idempotent: schreibt nur, wenn die Mitgliedschaft noch fehlt.
/// </summary>
public sealed class AdminClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminClaimsTransformation> _logger;

    public AdminClaimsTransformation(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration config,
        ILogger<AdminClaimsTransformation> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Bereits Admin? Dann nichts zu tun.
        if (principal.IsInRole(AdminInitializer.AdminRole))
            return principal;

        var email = _userManager.GetUserName(principal) ?? principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return principal;

        var adminEmails = _config.GetSection("Admin:Emails").Get<string[]>() ?? [];
        if (!adminEmails.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase)))
            return principal;

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return principal;

        if (!await _roleManager.RoleExistsAsync(AdminInitializer.AdminRole))
            await _roleManager.CreateAsync(new IdentityRole(AdminInitializer.AdminRole));

        if (!await _userManager.IsInRoleAsync(user, AdminInitializer.AdminRole))
        {
            await _userManager.AddToRoleAsync(user, AdminInitializer.AdminRole);
            _logger.LogInformation("Benutzer {Email} automatisch zur Rolle '{Role}' befördert (Admin:Emails).",
                email, AdminInitializer.AdminRole);
        }

        // Rollen-Claim sofort ergänzen, damit die Berechtigung ohne Neustart wirkt.
        if (principal.Identity is ClaimsIdentity id)
            id.AddClaim(new Claim(id.RoleClaimType, AdminInitializer.AdminRole));

        return principal;
    }
}
