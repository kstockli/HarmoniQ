namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Dedup-Protokoll (Wiederkehr-Schleife, UX-Spec 4.2): welcher Digest-Baustein (Typ + EntitätsId)
/// wurde einem Konto bereits geschickt. Verhindert Wiederholungen und leere Digests. Wird beim
/// tatsächlichen Versand (E-Mail-/Push-Adapter) geschrieben; die Zusammenstellung liest es zum Filtern.
/// </summary>
public class BenachrichtigungGesendet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BenutzerId { get; set; } = string.Empty;
    public ApplicationUser? Benutzer { get; set; }

    public BenachrichtigungTyp Typ { get; set; }

    /// <summary>Bezugs-Entität (Konzert-, Video-Id …), passend zum <see cref="Typ"/>.</summary>
    public Guid EntitaetId { get; set; }

    public DateTime GesendetAm { get; set; } = DateTime.UtcNow;
}
