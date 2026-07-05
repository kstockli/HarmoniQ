namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Web-Push-Anmeldung eines Geräts/Browsers für ein Konto (Wiederkehr-Schleife, UX-Spec 4.2).
/// Ein Konto kann mehrere haben (Handy, Laptop …). Enthält den Push-Endpoint des Browsers plus die
/// Verschlüsselungs-Schlüssel (p256dh/auth), die der Web-Push-Versand (VAPID) benötigt.
/// </summary>
public class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BenutzerId { get; set; } = string.Empty;
    public ApplicationUser? Benutzer { get; set; }

    /// <summary>Eindeutiger Push-Endpoint (URL des Push-Dienstes des Browsers).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Öffentlicher Client-Schlüssel (Base64url) aus der Browser-Subscription.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Auth-Secret (Base64url) aus der Browser-Subscription.</summary>
    public string Auth { get; set; } = string.Empty;
}
