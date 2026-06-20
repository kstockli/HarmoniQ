using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Verwaltung von Freundschaften zwischen Personen (gegenseitig, mit Status). Eine bestätigte
/// Freundschaft macht beide Personen füreinander voll sichtbar (siehe <see cref="PersonenSicht"/>).
/// </summary>
public static class FreundschaftService
{
    public enum Beziehung { Keine, AnfrageVonMir, AnfrageAnMich, Befreundet }

    /// <summary>Die mit dem Konto verknüpfte Person (oder null).</summary>
    public static async Task<Guid?> MeinePersonIdAsync(ApplicationDbContext db, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        var id = await db.Personen.Where(p => p.BenutzerId == userId).Select(p => p.Id).FirstOrDefaultAsync();
        return id == Guid.Empty ? null : id;
    }

    /// <summary>Aktuelle, nicht abgelehnte Verbindung zwischen zwei Personen (für die UI).</summary>
    public static async Task<(Beziehung Status, Guid? FreundschaftId)> BeziehungAsync(
        ApplicationDbContext db, Guid meineId, Guid andereId)
    {
        var f = await db.Freundschaften.FirstOrDefaultAsync(x =>
            x.Status != FreundschaftStatus.Abgelehnt &&
            ((x.AnfragerPersonId == meineId && x.EmpfaengerPersonId == andereId) ||
             (x.AnfragerPersonId == andereId && x.EmpfaengerPersonId == meineId)));
        if (f == null) return (Beziehung.Keine, null);
        if (f.Status == FreundschaftStatus.Bestaetigt) return (Beziehung.Befreundet, f.Id);
        return (f.AnfragerPersonId == meineId ? Beziehung.AnfrageVonMir : Beziehung.AnfrageAnMich, f.Id);
    }

    /// <summary>Sendet eine Freundschaftsanfrage (idempotent; reaktiviert eine abgelehnte).
    /// Bei einer neuen/reaktivierten Anfrage wird die empfangende Person per E-Mail benachrichtigt.</summary>
    public static async Task AnfrageSendenAsync(ApplicationDbContext db, Guid anfragerId, Guid empfaengerId,
        IBenachrichtigungsMail? mailer = null, string? freundeUrl = null)
    {
        if (anfragerId == empfaengerId) return;

        // Bestehende Verbindung in beliebiger Richtung suchen.
        var f = await db.Freundschaften.FirstOrDefaultAsync(x =>
            (x.AnfragerPersonId == anfragerId && x.EmpfaengerPersonId == empfaengerId) ||
            (x.AnfragerPersonId == empfaengerId && x.EmpfaengerPersonId == anfragerId));

        var benachrichtigen = false;
        if (f == null)
        {
            db.Freundschaften.Add(new Freundschaft
            {
                AnfragerPersonId = anfragerId,
                EmpfaengerPersonId = empfaengerId,
                Status = FreundschaftStatus.Offen
            });
            await db.SaveChangesAsync();
            benachrichtigen = true;
        }
        else if (f.Status == FreundschaftStatus.Abgelehnt)
        {
            // Neu aufrollen: Richtung auf aktuelle:n Anfrager:in setzen, wieder offen.
            f.AnfragerPersonId = anfragerId;
            f.EmpfaengerPersonId = empfaengerId;
            f.Status = FreundschaftStatus.Offen;
            f.ErstelltAm = DateTime.UtcNow;
            f.EntschiedenAm = null;
            await db.SaveChangesAsync();
            benachrichtigen = true;
        }
        // Offen/Bestätigt: nichts zu tun.

        if (benachrichtigen)
        {
            var anfragerName = await db.Personen.Where(p => p.Id == anfragerId).Select(p => p.Name).FirstOrDefaultAsync() ?? "Jemand";
            var body = $"<p><strong>{Enc(anfragerName)}</strong> möchte sich auf HarmoniQ mit dir vernetzen.</p>"
                       + LinkAbsatz(freundeUrl, "Anfrage ansehen");
            await MailAnPersonAsync(db, mailer, empfaengerId, "Neue Freundschaftsanfrage auf HarmoniQ", body);
        }
    }

    /// <summary>Bestätigt eine offene Anfrage, schreibt ein Feed-Ereignis und benachrichtigt
    /// die anfragende Person per E-Mail.</summary>
    public static async Task BestaetigenAsync(ApplicationDbContext db, Guid freundschaftId,
        IBenachrichtigungsMail? mailer = null, string? freundeUrl = null)
    {
        var f = await db.Freundschaften.FindAsync(freundschaftId);
        if (f == null || f.Status != FreundschaftStatus.Offen) return;
        f.Status = FreundschaftStatus.Bestaetigt;
        f.EntschiedenAm = DateTime.UtcNow;

        // „X ist jetzt mit Y befreundet“ – Akteur = bestätigende Person, Neben = anfragende.
        db.Aktivitaeten.Add(new Aktivitaet
        {
            AkteurPersonId = f.EmpfaengerPersonId,
            Typ = AktivitaetTyp.FreundschaftBestaetigt,
            NebenPersonId = f.AnfragerPersonId
        });
        await db.SaveChangesAsync();

        var empfaengerName = await db.Personen.Where(p => p.Id == f.EmpfaengerPersonId).Select(p => p.Name).FirstOrDefaultAsync() ?? "Jemand";
        var body = $"<p><strong>{Enc(empfaengerName)}</strong> hat deine Freundschaftsanfrage angenommen.</p>"
                   + LinkAbsatz(freundeUrl, "Zu deinen Freunden");
        await MailAnPersonAsync(db, mailer, f.AnfragerPersonId, "Deine Freundschaftsanfrage wurde angenommen", body);
    }

    /// <summary>Sendet eine Mail an die mit der Person verknüpfte (E-Mail-bestätigte) Kontoadresse.
    /// Fehler beim Versand brechen den Ablauf nicht ab.</summary>
    private static async Task MailAnPersonAsync(ApplicationDbContext db, IBenachrichtigungsMail? mailer,
        Guid personId, string subject, string htmlBody)
    {
        if (mailer == null) return;
        var konto = await db.Personen.Where(p => p.Id == personId)
            .Select(p => new
            {
                Email = p.Benutzer != null ? p.Benutzer.Email : null,
                Confirmed = p.Benutzer != null && p.Benutzer.EmailConfirmed
            })
            .FirstOrDefaultAsync();
        if (konto?.Email is { Length: > 0 } email && konto.Confirmed)
        {
            try { await mailer.SendAsync(email, subject, htmlBody); }
            catch { /* Benachrichtigung ist Best-Effort; Freundschaft bleibt gespeichert. */ }
        }
    }

    private static string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);
    private static string LinkAbsatz(string? url, string text) =>
        string.IsNullOrWhiteSpace(url) ? "" : $"<p><a href=\"{url}\">{Enc(text)}</a></p>";

    public static async Task AblehnenAsync(ApplicationDbContext db, Guid freundschaftId)
    {
        var f = await db.Freundschaften.FindAsync(freundschaftId);
        if (f == null || f.Status != FreundschaftStatus.Offen) return;
        f.Status = FreundschaftStatus.Abgelehnt;
        f.EntschiedenAm = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Löst eine bestätigte Freundschaft (oder einen Antrag).</summary>
    public static async Task EntfernenAsync(ApplicationDbContext db, Guid freundschaftId)
    {
        var f = await db.Freundschaften.FindAsync(freundschaftId);
        if (f == null) return;
        db.Freundschaften.Remove(f);
        await db.SaveChangesAsync();
    }
}
