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
}
