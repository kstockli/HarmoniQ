using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Verknüpft ein Benutzerkonto direkt mit einer Person („das bin ich").
///
/// VORÜBERGEHEND: Die Verknüpfung wird sofort **auto-bestätigt** (kein Admin-Schritt). Der
/// <see cref="PersonAnspruch"/> wird zur Nachvollziehbarkeit mit Status <c>Genehmigt</c> protokolliert.
/// Zum Reaktivieren der manuellen Prüfung: in den Aufrufern wieder einen Antrag mit Status
/// <c>Offen</c> anlegen (statt <see cref="DirektVerknuepfenAsync"/>) – die Admin-Queue
/// <c>/admin/verknuepfungen</c> bleibt dafür erhalten.
/// </summary>
public static class VerknuepfungService
{
    /// <summary>Sichtbare (öffentliche) Rollen laut Datenschutz-Konzept (Block 2): Claim braucht ein
    /// Verifizierungs-Gate. Zuhörer:in/Musikant:in sind nicht öffentlich → weicher Sofort-Claim.</summary>
    public static bool IstSichtbareRolle(Person person)
        => person.Sichtbarkeit == Sichtbarkeit.Oeffentlich
        || person.Rollen.Any(r => r.Rolle is PersonRolleTyp.Komponist or PersonRolleTyp.Dirigent);

    /// <summary>
    /// Beansprucht eine Person („das bin ich") mit Verifizierungs-Gate (Modell B):
    /// Bei <b>sichtbarer Rolle</b> (Dirigent:in/Komponist:in/öffentlich) wird nur ein <b>offener</b>
    /// <see cref="PersonAnspruch"/> angelegt (Prüfung via <c>/admin/verknuepfungen</c>), NICHT sofort
    /// verknüpft. Bei nicht-sichtbaren Rollen wird direkt verknüpft (weicher Merge).
    /// </summary>
    public static async Task<(bool Ok, bool Sofort, string Meldung)> BeanspruchenAsync(
        ApplicationDbContext db, Guid personId, string userId, string? begruendung)
    {
        if (await db.Personen.AnyAsync(p => p.BenutzerId == userId && p.Id != personId))
            return (false, false, "Dein Konto ist bereits mit einer anderen Person verknüpft.");

        var person = await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null) return (false, false, "Person nicht gefunden.");
        if (person.BenutzerId == userId) return (true, true, "Du bist bereits mit dieser Person verknüpft.");
        if (person.BenutzerId != null) return (false, false, "Diese Person ist bereits mit einem anderen Konto verknüpft.");

        if (!IstSichtbareRolle(person))
        {
            var (ok, meldung) = await DirektVerknuepfenAsync(db, personId, userId, begruendung);
            return (ok, ok, meldung);
        }

        // Gate: offenen Antrag anlegen (nicht verknüpfen), Dubletten vermeiden.
        var offenVorhanden = await db.PersonAnsprueche
            .AnyAsync(a => a.PersonId == personId && a.BenutzerId == userId && a.Status == PersonAnspruchStatus.Offen);
        if (!offenVorhanden)
        {
            db.PersonAnsprueche.Add(new PersonAnspruch
            {
                PersonId = personId,
                BenutzerId = userId,
                Begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim(),
                Status = PersonAnspruchStatus.Offen
            });
            await db.SaveChangesAsync();
        }
        return (true, false, "Weil diese Person öffentlich sichtbar ist (z. B. Dirigent:in/Komponist:in), "
            + "wird dein Anspruch kurz geprüft, bevor er aktiv wird.");
    }

    public static async Task<(bool Ok, string Meldung)> DirektVerknuepfenAsync(
        ApplicationDbContext db, Guid personId, string userId, string? begruendung)
    {
        // Konto darf nicht bereits mit einer anderen Person verknüpft sein (BenutzerId ist unique).
        if (await db.Personen.AnyAsync(p => p.BenutzerId == userId && p.Id != personId))
            return (false, "Dein Konto ist bereits mit einer anderen Person verknüpft.");

        var person = await db.Personen.FindAsync(personId);
        if (person == null) return (false, "Person nicht gefunden.");

        if (person.BenutzerId == userId)
            return (true, "Du bist bereits mit dieser Person verknüpft.");
        if (person.BenutzerId != null)
            return (false, "Diese Person ist bereits mit einem anderen Konto verknüpft.");

        person.BenutzerId = userId;
        db.PersonAnsprueche.Add(new PersonAnspruch
        {
            PersonId = personId,
            BenutzerId = userId,
            Begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim(),
            Status = PersonAnspruchStatus.Genehmigt,
            EntschiedenAm = DateTime.UtcNow
        });

        // Evtl. weitere offene Anträge derselben Person hinfällig machen.
        var weitere = await db.PersonAnsprueche
            .Where(a => a.PersonId == personId && a.Status == PersonAnspruchStatus.Offen)
            .ToListAsync();
        foreach (var w in weitere) { w.Status = PersonAnspruchStatus.Abgelehnt; w.EntschiedenAm = DateTime.UtcNow; }

        await db.SaveChangesAsync();

        // E-Mail-Link der Person mit der Konto-E-Mail synchronisieren.
        var kontoEmail = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        await PersonLinkSync.SyncEmailAsync(db, userId, kontoEmail);

        return (true, "Mit deiner Person verknüpft.");
    }
}
