using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Hält den E-Mail-<see cref="PersonLink"/> einer mit einem Konto verknüpften Person
/// synchron mit der E-Mail-Adresse des Benutzerkontos.
/// </summary>
public static class PersonLinkSync
{
    /// <summary>
    /// Setzt/aktualisiert den E-Mail-Link der Person, die mit <paramref name="userId"/>
    /// verknüpft ist, auf <paramref name="email"/>. Speichert selbst.
    /// </summary>
    public static async Task SyncEmailAsync(ApplicationDbContext db, string? userId, string? email)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var person = await db.Personen.Include(p => p.Links)
            .FirstOrDefaultAsync(p => p.BenutzerId == userId);
        if (person == null) return;

        var vorhanden = person.Links.FirstOrDefault(l => l.Typ == LinkTyp.EMail);
        if (string.IsNullOrWhiteSpace(email))
        {
            if (vorhanden != null) { person.Links.Remove(vorhanden); db.PersonLinks.Remove(vorhanden); }
        }
        else if (vorhanden != null)
        {
            if (vorhanden.Url != email) vorhanden.Url = email;
        }
        else
        {
            var neu = new PersonLink { PersonId = person.Id, Typ = LinkTyp.EMail, Url = email };
            person.Links.Add(neu);
            db.PersonLinks.Add(neu);
        }
        await db.SaveChangesAsync();
    }
}
