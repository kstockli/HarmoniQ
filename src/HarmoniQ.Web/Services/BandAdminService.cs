using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Band-skopierte Admin-Rolle (UX-Spec Block 5): prüft/verwaltet, welche Konten welche Band pflegen
/// dürfen. Der globale Admin (Rolle „Admin") darf immer alles – das wird beim Aufrufer geprüft.
/// </summary>
public static class BandAdminService
{
    /// <summary>Ist dieses Konto globaler Admin? DB-basiert (AspNetUserRoles ⋈ Roles) – zuverlässig
    /// in Prerender UND interaktivem Circuit (anders als <c>ClaimsPrincipal.IsInRole</c>, das im
    /// interaktiven Server-Kontext die Rolle nicht immer trägt).</summary>
    public static Task<bool> IstGlobalAdminAsync(ApplicationDbContext db, string? userId)
        => string.IsNullOrEmpty(userId)
            ? Task.FromResult(false)
            : (from ur in db.UserRoles
               join r in db.Roles on ur.RoleId equals r.Id
               where ur.UserId == userId && r.Name == "Admin"
               select ur.UserId).AnyAsync();

    /// <summary>Ist dieses Konto Band-Admin der Band?</summary>
    public static Task<bool> IstBandAdminAsync(ApplicationDbContext db, string? userId, Guid bandId)
        => string.IsNullOrEmpty(userId)
            ? Task.FromResult(false)
            : db.BandAdministratoren.AnyAsync(a => a.BenutzerId == userId && a.BandId == bandId);

    /// <summary>Alle Bands, die dieses Konto verwaltet (leer wenn keine).</summary>
    public static async Task<List<Guid>> AdminBandIdsAsync(ApplicationDbContext db, string? userId)
        => string.IsNullOrEmpty(userId)
            ? []
            : await db.BandAdministratoren.Where(a => a.BenutzerId == userId).Select(a => a.BandId).ToListAsync();

    /// <summary>Ernennt ein Konto zum Band-Admin (idempotent). Speichert.</summary>
    public static async Task ErnennenAsync(ApplicationDbContext db, string userId, Guid bandId)
    {
        if (await db.BandAdministratoren.AnyAsync(a => a.BenutzerId == userId && a.BandId == bandId)) return;
        db.BandAdministratoren.Add(new BandAdministrator { BenutzerId = userId, BandId = bandId });
        await db.SaveChangesAsync();
    }

    /// <summary>Entzieht die Band-Admin-Rolle. Speichert.</summary>
    public static async Task EntziehenAsync(ApplicationDbContext db, Guid administratorId)
    {
        await db.BandAdministratoren.Where(a => a.Id == administratorId).ExecuteDeleteAsync();
    }
}
