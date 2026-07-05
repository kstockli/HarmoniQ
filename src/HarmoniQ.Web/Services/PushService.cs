using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebPush;
using HarmoniQ.Web.Data;
using PushAbo = HarmoniQ.Web.Data.Models.PushSubscription;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Web-Push-Versand (PWA-Push, UX-Spec 4.2) über VAPID. Verschickt eine Notification an alle
/// Push-Anmeldungen eines Kontos; abgelaufene/ungültige Anmeldungen (HTTP 404/410) werden entfernt.
/// Konfiguration unter <c>Push:PublicKey</c> / <c>Push:PrivateKey</c> (Secret) / <c>Push:Subject</c>.
/// </summary>
public static class PushService
{
    public static bool IstKonfiguriert(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Push:PublicKey"]) && !string.IsNullOrWhiteSpace(config["Push:PrivateKey"]);

    /// <summary>Sendet eine Notification an alle Geräte des Kontos. Gibt die Anzahl erreichter Geräte zurück.</summary>
    public static async Task<int> SendeAnKontoAsync(
        ApplicationDbContext db, IConfiguration config, string userId, string titel, string text, string url)
    {
        if (!IstKonfiguriert(config)) return 0;

        var abos = await db.PushSubscriptions.Where(s => s.BenutzerId == userId).ToListAsync();
        if (abos.Count == 0) return 0;

        var vapid = new VapidDetails(
            config["Push:Subject"] ?? "mailto:admin@harmoniq.q-no.ch",
            config["Push:PublicKey"], config["Push:PrivateKey"]);
        var client = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { title = titel, body = text, url });

        var erreicht = 0;
        var abgelaufen = new List<PushAbo>();
        foreach (var a in abos)
        {
            try
            {
                await client.SendNotificationAsync(new WebPush.PushSubscription(a.Endpoint, a.P256dh, a.Auth), payload, vapid);
                erreicht++;
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                                       or System.Net.HttpStatusCode.Gone)
            {
                abgelaufen.Add(a);   // Gerät hat sich abgemeldet → aufräumen
            }
            catch { /* transienter Fehler eines Geräts – andere weiter bedienen */ }
        }

        if (abgelaufen.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(abgelaufen);
            await db.SaveChangesAsync();
        }
        return erreicht;
    }
}
