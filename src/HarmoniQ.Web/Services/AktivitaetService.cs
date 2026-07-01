using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Schreibt Feed-Ereignisse in die append-only <see cref="Aktivitaet"/>-Tabelle.
/// </summary>
public static class AktivitaetService
{
    /// <summary>Die mit dem Konto verknüpfte Person (oder null).</summary>
    public static async Task<Guid?> PersonIdAsync(ApplicationDbContext db, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        var id = await db.Personen.Where(p => p.BenutzerId == userId).Select(p => p.Id).FirstOrDefaultAsync();
        return id == Guid.Empty ? null : id;
    }

    /// <summary>Fügt ein Ereignis dem Context hinzu (ohne SaveChanges – der Aufrufer speichert).</summary>
    public static void Hinzufuegen(ApplicationDbContext db, Guid akteurPersonId, AktivitaetTyp typ,
        AktivitaetZielTyp? zielTyp = null, Guid? zielId = null, Guid? nebenPersonId = null, string? text = null)
    {
        db.Aktivitaeten.Add(new Aktivitaet
        {
            AkteurPersonId = akteurPersonId,
            Typ = typ,
            ZielTyp = zielTyp,
            ZielId = zielId,
            NebenPersonId = nebenPersonId,
            Text = text
        });
    }

    /// <summary>Komfort: löst die Person zum Benutzer auf, schreibt das Ereignis und speichert.
    /// Tut nichts, wenn der Benutzer keine verknüpfte Person hat.</summary>
    public static async Task ProtokolliereFuerBenutzerAsync(ApplicationDbContext db, string? userId,
        AktivitaetTyp typ, AktivitaetZielTyp? zielTyp = null, Guid? zielId = null)
    {
        var pid = await PersonIdAsync(db, userId);
        if (pid is not Guid id) return;
        Hinzufuegen(db, id, typ, zielTyp, zielId);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Hält das Feed-Ereignis „war beim Konzert" konsistent zur Tagebuch-Sichtbarkeit: legt es an,
    /// wenn der Besuch geteilt ist (Sichtbarkeit ≠ NurIch), und entfernt es sonst. Nur die
    /// (stabile) Anwesenheit landet im Feed – Notiz/Bewertungen bleiben auf der Konzertseite.
    /// Tut nichts, wenn der Benutzer keine verknüpfte Person hat (Feed ist personen-basiert).
    /// </summary>
    public static async Task SyncKonzertBesuchFeedAsync(ApplicationDbContext db, string? userId,
        Guid konzertId, bool geteilt)
    {
        var pid = await PersonIdAsync(db, userId);
        if (pid is not Guid akteur) return;

        var vorhanden = await db.Aktivitaeten.FirstOrDefaultAsync(a =>
            a.AkteurPersonId == akteur && a.Typ == AktivitaetTyp.KonzertBesucht && a.ZielId == konzertId);

        if (geteilt && vorhanden is null)
            Hinzufuegen(db, akteur, AktivitaetTyp.KonzertBesucht, AktivitaetZielTyp.Konzert, konzertId);
        else if (!geteilt && vorhanden is not null)
            db.Aktivitaeten.Remove(vorhanden);
        else
            return;

        await db.SaveChangesAsync();
    }
}
