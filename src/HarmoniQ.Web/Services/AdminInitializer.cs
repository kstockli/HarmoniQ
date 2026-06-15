using Microsoft.AspNetCore.Identity;
using HarmoniQ.Web.Data;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Stellt beim Start sicher, dass die Rolle "Admin" existiert und die in der
/// Konfiguration ("Admin:Emails") hinterlegten Benutzer dieser Rolle angehören.
/// Promotion greift, sobald der jeweilige Benutzer existiert (z. B. nach Registrierung).
/// </summary>
public static class AdminInitializer
{
    public const string AdminRole = "Admin";

    public static async Task EnsureAdminsAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
            logger.LogInformation("Rolle '{Role}' erstellt.", AdminRole);
        }

        var adminEmails = config.GetSection("Admin:Emails").Get<string[]>() ?? [];
        foreach (var email in adminEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                logger.LogInformation("Admin-Kandidat {Email} noch nicht registriert – wird bei nächstem Start erneut geprüft.", email);
                continue;
            }
            if (!await userManager.IsInRoleAsync(user, AdminRole))
            {
                await userManager.AddToRoleAsync(user, AdminRole);
                logger.LogInformation("Benutzer {Email} wurde zur Rolle '{Role}' hinzugefügt.", email, AdminRole);
            }
        }
    }
}
