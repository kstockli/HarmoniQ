using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Band-skopierte Admin-Rolle (UX-Spec Block 5): prüft/verwaltet, welche Konten welche Band pflegen
/// dürfen, inkl. Einladung von Noch-nicht-User:innen (§5.3.2). Der globale Admin darf immer alles.
/// </summary>
public static class BandAdminService
{
    /// <summary>Ist dieses Konto globaler Admin? DB-basiert (AspNetUserRoles ⋈ Roles) – zuverlässig
    /// in Prerender UND interaktivem Circuit (anders als <c>ClaimsPrincipal.IsInRole</c>).</summary>
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

    /// <summary>Ernennt ein Konto zum Band-Admin (idempotent) und lässt es der Band automatisch
    /// <b>folgen</b> (UX-Spec: Band-Admin = auch Interessent). Speichert.</summary>
    public static async Task ErnennenAsync(ApplicationDbContext db, string userId, Guid bandId)
    {
        if (!await db.BandAdministratoren.AnyAsync(a => a.BenutzerId == userId && a.BandId == bandId))
        {
            db.BandAdministratoren.Add(new BandAdministrator { BenutzerId = userId, BandId = bandId });
            await db.SaveChangesAsync();
        }
        await AutoFolgenAsync(db, userId, bandId);
    }

    /// <summary>Entzieht die Band-Admin-Rolle. Speichert. (Das Folgen bleibt bestehen.)</summary>
    public static Task EntziehenAsync(ApplicationDbContext db, Guid administratorId)
        => db.BandAdministratoren.Where(a => a.Id == administratorId).ExecuteDeleteAsync();

    /// <summary>Band-Admin der verknüpften Person → automatisch Band folgen (falls Person vorhanden
    /// und noch nicht Mitglied/folgend).</summary>
    private static async Task AutoFolgenAsync(ApplicationDbContext db, string userId, Guid bandId)
    {
        if (await BandFolgenService.PersonIdAsync(db, userId) is not Guid pid) return;
        if (await BandFolgenService.IstMitgliedAsync(db, pid, bandId)) return;
        if (await BandFolgenService.FolgtAsync(db, pid, bandId)) return;
        db.BandInteressen.Add(new BandInteresse { PersonId = pid, BandId = bandId });
        await db.SaveChangesAsync();
    }

    // ── Einladungen (§5.3.2) ────────────────────────────────────────────────────────

    /// <summary>Ernennt das Konto mit dieser E-Mail direkt (falls vorhanden) oder legt eine
    /// Einladung an und mailt einen Einmal-Link. Gibt eine User-Meldung zurück.</summary>
    public static async Task<string> ErnennenOderEinladenAsync(
        ApplicationDbContext db, IBenachrichtigungsMail? mailer, string baseUrl,
        Guid bandId, string email, string? eingeladenVon)
    {
        email = email.Trim();
        if (string.IsNullOrWhiteSpace(email)) return "Bitte eine E-Mail eingeben.";
        var norm = email.ToUpperInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == norm || u.Email == email);
        if (user != null)
        {
            await ErnennenAsync(db, user.Id, bandId);
            return $"{email} ist jetzt Band-Admin.";
        }

        // Kein Konto → Einladung (offene wiederverwenden statt duplizieren).
        var offen = await db.BandAdminEinladungen.FirstOrDefaultAsync(e =>
            e.BandId == bandId && e.Email == email && e.Status == BandAdminEinladungStatus.Offen);
        if (offen == null)
        {
            offen = new BandAdminEinladung
            {
                Email = email, BandId = bandId, EingeladenVon = eingeladenVon,
                AblaufAm = DateTime.UtcNow.AddDays(14)
            };
            db.BandAdminEinladungen.Add(offen);
        }
        else
        {
            offen.AblaufAm = DateTime.UtcNow.AddDays(14);   // verlängern + erneut senden
        }
        await db.SaveChangesAsync();

        var bandName = await db.Bands.Where(b => b.Id == bandId).Select(b => b.Name).FirstOrDefaultAsync() ?? "einen Verein";
        var link = $"{baseUrl}einladung/{offen.Token}";
        var body = EinladungsMailHtml(bandName, link, baseUrl);
        if (mailer != null)
        {
            try { await mailer.SendAsync(email, $"Einladung: {bandName} auf HarmoniQ verwalten", body); }
            catch { /* Best-Effort: Einladung bleibt gespeichert, Link ist gültig. */ }
        }
        return $"Einladung an {email} gesendet.";
    }

    /// <summary>Aufgewertete Einladungs-Mail (HTML, e-mail-sicher inline gestylt): erklärt aus Sicht
    /// der/des Vereinsverantwortlichen den Nutzen + wirbt für HarmoniQ.</summary>
    private static string EinladungsMailHtml(string bandName, string link, string baseUrl)
    {
        var b = System.Net.WebUtility.HtmlEncode(bandName);
        var app = baseUrl.TrimEnd('/');
        return $@"
<div style=""font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#222;line-height:1.6;"">
  <div style=""background:#2d1657;padding:20px 24px;border-radius:12px 12px 0 0;"">
    <div style=""color:#D8B4FE;font-size:22px;font-weight:bold;"">HarmoniQ</div>
    <div style=""color:#C4B0D8;font-size:12px;letter-spacing:2px;"">DIE BLASMUSIK-DATENBANK</div>
  </div>
  <div style=""border:1px solid #e5e0ef;border-top:none;border-radius:0 0 12px 12px;padding:24px;"">
    <p style=""font-size:17px;margin:0 0 12px;""><strong>Du wurdest eingeladen, {b} auf HarmoniQ zu verwalten.</strong></p>
    <p style=""margin:0 0 16px;"">HarmoniQ ist die Plattform für die Schweizer Blasmusik-Szene – Konzerte,
      Stücke, Aufnahmen und die Menschen dahinter. Als Verein-Verwalter:in pflegst du den Auftritt
      deines Vereins selbst und erreichst dein Publikum – kostenlos.</p>
    <p style=""margin:0 0 8px;font-weight:bold;color:#5B21B6;"">Dein Nutzen als Vereinsverantwortliche:r:</p>
    <ul style=""margin:0 0 20px;padding-left:20px;"">
      <li><strong>Kostenlose Vereins-Präsenz</strong> – Porträt, Logo, Geschichte und Links, immer aktuell und auffindbar.</li>
      <li><strong>Konzerte ankündigen</strong> – eure Auftritte erscheinen bei Interessierten in der Nähe und bei euren Follower:innen.</li>
      <li><strong>Mitglieder &amp; Vorstand pflegen</strong> – Besetzung und Funktionen selbst aktuell halten.</li>
      <li><strong>Aufnahmen &amp; Repertoire</strong> – Videos und gespielte Stücke sammeln und zeigen.</li>
      <li><strong>Fans gewinnen</strong> – Leute folgen eurem Verein und erhalten eure Neuigkeiten automatisch.</li>
    </ul>
    <p style=""text-align:center;margin:24px 0;"">
      <a href=""{link}"" style=""background:#D4AF37;color:#0e0018;text-decoration:none;font-weight:bold;padding:14px 28px;border-radius:8px;display:inline-block;"">Einladung annehmen</a>
    </p>
    <p style=""font-size:13px;color:#666;margin:16px 0 0;"">Du hast noch kein Konto? Der Link führt dich durch die
      kostenlose Registrierung (Passwort oder Google/Microsoft) – danach bist du direkt bei deiner Band.
      Falls du diese Einladung nicht erwartet hast, kannst du sie einfach ignorieren.</p>
  </div>
  <p style=""text-align:center;font-size:12px;color:#999;margin:12px 0;""><a href=""{app}"" style=""color:#999;"">{app}</a></p>
</div>";
    }

    /// <summary>Offene, gültige Einladungen einer Band (für die Verwaltungs-Anzeige).</summary>
    public static Task<List<BandAdminEinladung>> OffeneEinladungenAsync(ApplicationDbContext db, Guid bandId)
        => db.BandAdminEinladungen
            .Where(e => e.BandId == bandId && e.Status == BandAdminEinladungStatus.Offen)
            .OrderBy(e => e.Email).ToListAsync();

    public static Task StornierenAsync(ApplicationDbContext db, Guid einladungId)
        => db.BandAdminEinladungen.Where(e => e.Id == einladungId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, BandAdminEinladungStatus.Storniert));

    /// <summary>Einladung per Token (inkl. Band); null wenn nicht gefunden.</summary>
    public static Task<BandAdminEinladung?> EinladungPerTokenAsync(ApplicationDbContext db, string? token)
        => string.IsNullOrWhiteSpace(token)
            ? Task.FromResult<BandAdminEinladung?>(null)
            : db.BandAdminEinladungen.Include(e => e.Band).FirstOrDefaultAsync(e => e.Token == token);

    /// <summary>Nimmt die Einladung für das angegebene Konto an: legt Band-Admin an (+ folgen),
    /// markiert die Einladung als angenommen. Gibt die BandId zurück (null wenn ungültig).</summary>
    public static async Task<Guid?> AnnehmenAsync(ApplicationDbContext db, string token, string userId)
    {
        var e = await db.BandAdminEinladungen.FirstOrDefaultAsync(x => x.Token == token);
        if (e is null || !e.IstOffenUndGueltig) return null;
        await ErnennenAsync(db, userId, e.BandId);
        e.Status = BandAdminEinladungStatus.Angenommen;
        await db.SaveChangesAsync();
        return e.BandId;
    }
}
