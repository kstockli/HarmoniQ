using System.Text;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// E-Mail-Adapter der Wiederkehr-Schleife (UX-Spec 4.2, „Wochenüberblick"): baut aus einem
/// <see cref="DigestService.Digest"/> die HTML-Mail, verschickt sie über <see cref="IBenachrichtigungsMail"/>
/// und protokolliert die versendeten Bausteine im Dedup-Log (<see cref="BenachrichtigungGesendet"/>).
/// Kanal-neutrale Zusammenstellung liefert <see cref="DigestService"/>; hier ist nur der E-Mail-Kanal.
/// </summary>
public static class WochenBenachrichtigung
{
    public record Ergebnis(bool Gesendet, int Anzahl, string Meldung);

    /// <summary>Verschickt den Wochenüberblick an EIN Konto über die aktiven Kanäle (E-Mail und/oder
    /// PWA-Push) – falls Inhalt vorhanden – und schreibt das Dedup-Log (nur bei tatsächlicher Zustellung).
    /// Legt fehlende Präferenzen mit Default an (opt-in, beide Kanäle).</summary>
    public static async Task<Ergebnis> VersendeAnKontoAsync(
        ApplicationDbContext db, IBenachrichtigungsMail mail, IConfiguration config, string basisUrl, string userId)
    {
        var pref = await BenachrichtigungService.HolenOderErstellenAsync(db, userId);
        if (!pref.EmailAktiv && !pref.PushAktiv) return new(false, 0, "Beide Kanäle sind abgeschaltet.");

        var digest = await DigestService.ErstelleAsync(db, userId);
        if (digest.Leer) return new(false, 0, "Nichts Neues – keine Benachrichtigung.");

        basisUrl = basisUrl.TrimEnd('/');
        var kanaele = new List<string>();

        // ── E-Mail ─────────────────────────────────────────────────────────────
        if (pref.EmailAktiv)
        {
            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(email))
            {
                var html = HtmlBauen(digest, basisUrl, pref.AbmeldeToken);
                await mail.SendAsync(email, "Deine Woche in der Blasmusik", html);
                kanaele.Add("E-Mail");
            }
        }

        // ── PWA-Push ─────────────────────────────────────────────────────────────
        if (pref.PushAktiv)
        {
            var erreicht = await PushService.SendeAnKontoAsync(db, config, userId,
                "Deine Woche in der Blasmusik", Zusammenfassung(digest), $"{basisUrl}/account/benachrichtigungen");
            if (erreicht > 0) kanaele.Add($"Push ({erreicht})");
        }

        if (kanaele.Count == 0) return new(false, 0, "Kein Kanal konnte zustellen (z. B. keine Push-Geräte).");

        // Dedup-Log: alle enthaltenen Bausteine als gesendet vermerken (kanalübergreifend).
        foreach (var p in digest.Alle)
            db.BenachrichtigungenGesendet.Add(new BenachrichtigungGesendet
            {
                BenutzerId = userId, Typ = p.Typ, EntitaetId = p.EntitaetId
            });
        await db.SaveChangesAsync();

        return new(true, digest.Total,
            $"Wochenüberblick mit {digest.Total} Beitrag/Beiträgen ausgelöst via {string.Join(" + ", kanaele)}.");
    }

    /// <summary>Verschickt an alle in Frage kommenden Konten (verknüpfte Person + interessierende Bands).
    /// Gibt zurück, an wie viele Konten tatsächlich zugestellt wurde.</summary>
    public static async Task<int> VersendeAlleAsync(
        ApplicationDbContext db, IBenachrichtigungsMail mail, IConfiguration config, string basisUrl,
        CancellationToken ct = default)
    {
        // Kandidaten: Konten mit verknüpfter Person (nur die können Bands folgen / Mitglied sein).
        var userIds = await db.Personen.Where(p => p.BenutzerId != null)
            .Select(p => p.BenutzerId!).Distinct().ToListAsync(ct);

        var gesendet = 0;
        foreach (var uid in userIds)
        {
            if (ct.IsCancellationRequested) break;
            var r = await VersendeAnKontoAsync(db, mail, config, basisUrl, uid);
            if (r.Gesendet) gesendet++;
        }
        return gesendet;
    }

    private static string Zusammenfassung(DigestService.Digest d)
    {
        var teile = new List<string>();
        if (d.Kommende.Count > 0) teile.Add($"{d.Kommende.Count} kommende Konzerte");
        if (d.Nachfragen.Count > 0) teile.Add($"{d.Nachfragen.Count} zum Eintragen");
        if (d.Videos.Count > 0) teile.Add($"{d.Videos.Count} neue Videos");
        if (d.Nahe.Count > 0) teile.Add($"{d.Nahe.Count} in deiner Nähe");
        return teile.Count > 0 ? string.Join(", ", teile) : "Neuigkeiten deiner Bands";
    }

    private static string HtmlBauen(DigestService.Digest d, string basis, Guid abmeldeToken)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#2b2b2b;\">");
        sb.Append("<h2 style=\"color:#7a5cc0;\">Deine Woche in der Blasmusik</h2>");
        sb.Append("<p style=\"color:#555;\">Was bei deinen und den von dir gefolgten Bands läuft:</p>");

        Abschnitt(sb, basis, "Kommende Konzerte", d.Kommende);
        Abschnitt(sb, basis, "Warst du dabei? – ins Tagebuch eintragen", d.Nachfragen);
        Abschnitt(sb, basis, "Neue Videos", d.Videos);
        Abschnitt(sb, basis, "Konzerte in deiner Nähe", d.Nahe);

        sb.Append("<hr style=\"border:none;border-top:1px solid #eee;margin:24px 0 12px;\" />");
        sb.Append("<p style=\"font-size:12px;color:#999;\">Du erhältst diese wöchentliche E-Mail, weil du "
            + "HarmoniQ-Benachrichtigungen aktiviert hast. ");
        sb.Append($"<a href=\"{basis}/benachrichtigungen/abmelden?token={abmeldeToken}\" style=\"color:#999;\">"
            + "E-Mails abbestellen</a> · ");
        sb.Append($"<a href=\"{basis}/account/benachrichtigungen\" style=\"color:#999;\">Einstellungen</a></p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void Abschnitt(StringBuilder sb, string basis, string titel, List<DigestService.Posten> posten)
    {
        if (posten.Count == 0) return;
        sb.Append($"<h3 style=\"color:#333;margin-bottom:6px;\">{Enc(titel)}</h3><ul style=\"padding-left:18px;margin-top:0;\">");
        foreach (var p in posten)
            sb.Append($"<li style=\"margin-bottom:6px;\"><a href=\"{basis}{p.Href}\" style=\"color:#7a5cc0;text-decoration:none;\">"
                + $"{Enc(p.Titel)}</a><br/><span style=\"font-size:13px;color:#777;\">{Enc(p.Detail)}</span></li>");
        sb.Append("</ul>");
    }

    private static string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);
}
