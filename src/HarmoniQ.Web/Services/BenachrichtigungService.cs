using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Verwaltet die Benachrichtigungs-Präferenzen eines Kontos (Wiederkehr-Schleife, UX-Spec 4.2):
/// Holen/Anlegen (Default: beide Kanäle an) und die tokenbasierte One-Click-Abmeldung des E-Mail-Kanals.
/// </summary>
public static class BenachrichtigungService
{
    /// <summary>Liefert die Präferenz-Zeile des Kontos; legt sie mit Defaults an, falls sie fehlt.</summary>
    public static async Task<BenachrichtigungPraeferenz> HolenOderErstellenAsync(ApplicationDbContext db, string userId)
    {
        var pref = await db.BenachrichtigungPraeferenzen.FirstOrDefaultAsync(p => p.BenutzerId == userId);
        if (pref == null)
        {
            pref = new BenachrichtigungPraeferenz { BenutzerId = userId };
            db.BenachrichtigungPraeferenzen.Add(pref);
            await db.SaveChangesAsync();
        }
        return pref;
    }

    /// <summary>Meldet den E-Mail-Kanal per Abmelde-Token ab (ohne Login). Push bleibt unberührt.</summary>
    public static async Task<(bool Ok, string? Email)> AbmeldenPerTokenAsync(ApplicationDbContext db, Guid token)
    {
        if (token == Guid.Empty) return (false, null);
        var pref = await db.BenachrichtigungPraeferenzen
            .Include(p => p.Benutzer)
            .FirstOrDefaultAsync(p => p.AbmeldeToken == token);
        if (pref == null) return (false, null);

        if (pref.EmailAktiv)
        {
            pref.EmailAktiv = false;
            await db.SaveChangesAsync();
        }
        return (true, pref.Benutzer?.Email);
    }
}
