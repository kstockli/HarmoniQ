using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Find-or-create für <see cref="Lokal"/>e: matcht den eingegebenen Namen gegen
/// <see cref="Lokal.Name"/> und <see cref="LokalAlias.Name"/> (case-insensitiv). Kein Treffer →
/// neues Lokal. Verhindert Dubletten beim Wizard/Import (analog Band/Stück). Siehe UX-Spec 4.3.
/// </summary>
public static class LokalService
{
    /// <summary>Sucht ein Lokal per Name/Alias oder legt es an; gibt die Id zurück (null bei leerem Namen).
    /// Speichert NICHT – der Aufrufer ruft SaveChanges.</summary>
    public static async Task<Guid?> FindeOderErstelleAsync(ApplicationDbContext db, string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        var lokal = await db.Lokale.FirstOrDefaultAsync(l => l.Name == name)
            ?? await db.Lokale.FirstOrDefaultAsync(l => l.Aliase.Any(a => a.Name == name))
            ?? db.Lokale.Local.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

        if (lokal == null)
        {
            lokal = new Lokal { Name = name };
            db.Lokale.Add(lokal);
        }
        return lokal.Id;
    }
}
