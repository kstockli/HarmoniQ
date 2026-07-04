namespace HarmoniQ.Web.Data;

/// <summary>
/// Zirkuit-gebundener (scoped) Träger der aktuell handelnden Person für die Audit-Spalten
/// (<c>createuser</c>/<c>modifyuser</c>). Wird pro Blazor-Circuit einmal aus dem Auth-State gesetzt
/// (siehe <c>MainLayout</c>). Auflösung des gespeicherten Namens:
/// eingeloggt → E-Mail · anonym (aktiver Circuit ohne Login) → „Anonym" · Hintergrund/kein Circuit → „System".
/// </summary>
public class BenutzerKontext
{
    public string? Email { get; private set; }
    /// <summary>true, sobald aus einem interaktiven Circuit gesetzt (unterscheidet Anonym von Hintergrund).</summary>
    public bool Aktiv { get; private set; }

    public void Setzen(string? email)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email;
        Aktiv = true;
    }

    /// <summary>Wert für die Audit-Spalten.</summary>
    public string AuditName => Aktiv ? (Email ?? "Anonym") : "System";
}
