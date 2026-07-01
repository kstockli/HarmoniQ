using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Führt ein Quell-Lokal in ein Ziel-Lokal zusammen: alle Konzerte werden umgehängt, leere
/// Ziel-Stammdaten aus der Quelle gefüllt, Quell-Name + dessen Aliase als <see cref="LokalAlias"/>
/// des Ziels gesichert, danach das Quell-Lokal gelöscht. Analog Band-/Stück-Merge. UX-Spec 4.3.
/// </summary>
public static class LokalMergeService
{
    public static async Task<(bool Ok, string Meldung)> MergeAsync(ApplicationDbContext db, Guid quelleId, Guid zielId)
    {
        if (quelleId == zielId) return (false, "Quelle und Ziel sind identisch.");

        var quelle = await db.Lokale.Include(l => l.Aliase).FirstOrDefaultAsync(l => l.Id == quelleId);
        var ziel = await db.Lokale.Include(l => l.Aliase).FirstOrDefaultAsync(l => l.Id == zielId);
        if (quelle == null || ziel == null) return (false, "Lokal nicht gefunden.");

        // Konzerte umhängen.
        foreach (var k in await db.Konzerte.Where(k => k.LokalId == quelleId).ToListAsync())
            k.LokalId = zielId;

        // Leere Ziel-Stammdaten aus der Quelle füllen.
        if (string.IsNullOrWhiteSpace(ziel.Saal)) ziel.Saal = quelle.Saal;
        if (string.IsNullOrWhiteSpace(ziel.Adresse)) ziel.Adresse = quelle.Adresse;
        if (string.IsNullOrWhiteSpace(ziel.Stadt)) ziel.Stadt = quelle.Stadt;
        if (string.IsNullOrWhiteSpace(ziel.Kanton)) ziel.Kanton = quelle.Kanton;
        if (string.IsNullOrWhiteSpace(ziel.Webseite)) ziel.Webseite = quelle.Webseite;
        ziel.Lat ??= quelle.Lat;
        ziel.Lng ??= quelle.Lng;

        // Quell-Name + Quell-Aliase als Aliase des Ziels sichern (ohne Dubletten).
        var bekannt = new HashSet<string>(
            ziel.Aliase.Select(a => a.Name).Append(ziel.Name), StringComparer.OrdinalIgnoreCase);
        void AliasSichern(string name)
        {
            name = name.Trim();
            if (name.Length > 0 && bekannt.Add(name))
                db.LokalAliase.Add(new LokalAlias { LokalId = zielId, Name = name });
        }
        AliasSichern(quelle.Name);
        foreach (var a in quelle.Aliase) AliasSichern(a.Name);

        db.Lokale.Remove(quelle);   // Quell-Aliase per Cascade.

        await db.SaveChangesAsync();
        return (true, $"„{quelle.Name}“ wurde in „{ziel.Name}“ zusammengeführt.");
    }
}
