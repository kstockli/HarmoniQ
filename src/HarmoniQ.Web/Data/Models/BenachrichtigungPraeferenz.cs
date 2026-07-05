namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Benachrichtigungs-Einstellungen eines Kontos (Wiederkehr-Schleife, UX-Spec 4.2). Die zwei Kanäle
/// sind <b>unabhängig</b> wählbar (nur E-Mail / nur Push / beides / keins). Opt-in beim Onboarding mit
/// Default an. Fehlt für ein Konto eine Zeile, gelten die Defaults (beide Kanäle an).
/// </summary>
public class BenachrichtigungPraeferenz : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Konto, dem die Einstellungen gehören (eindeutig).</summary>
    public string BenutzerId { get; set; } = string.Empty;
    public ApplicationUser? Benutzer { get; set; }

    /// <summary>Wochen-Digest per E-Mail.</summary>
    public bool EmailAktiv { get; set; } = true;

    /// <summary>Wochen-Digest per PWA-Push (wirkt nur mit vorhandener Push-Anmeldung).</summary>
    public bool PushAktiv { get; set; } = true;

    /// <summary>Token für die tokenbasierte One-Click-Abmeldung im Mail-Footer (ohne Login).</summary>
    public Guid AbmeldeToken { get; set; } = Guid.NewGuid();
}
