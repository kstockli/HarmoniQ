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

    /// <summary>Sendet eine Freundschaftsanfrage (idempotent; reaktiviert eine abgelehnte).</summary>
    public static async Task AnfrageSendenAsync(ApplicationDbContext db, Guid anfragerId, Guid empfaengerId)
    {
        if (anfragerId == empfaengerId) return;

        // Bestehende Verbindung in beliebiger Richtung suchen.
        var f = await db.Freundschaften.FirstOrDefaultAsync(x =>
            (x.AnfragerPersonId == anfragerId && x.EmpfaengerPersonId == empfaengerId) ||
            (x.AnfragerPersonId == empfaengerId && x.EmpfaengerPersonId == anfragerId));

        if (f == null)
        {
            db.Freundschaften.Add(new Freundschaft
            {
                AnfragerPersonId = anfragerId,
                EmpfaengerPersonId = empfaengerId,
                Status = FreundschaftStatus.Offen
            });
            await db.SaveChangesAsync();
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
        }
        // Offen/Bestätigt: nichts zu tun.
    }

    /// <summary>Bestätigt eine offene Anfrage und schreibt ein Feed-Ereignis.</summary>
    public static async Task BestaetigenAsync(ApplicationDbContext db, Guid freundschaftId)
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
    }

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
