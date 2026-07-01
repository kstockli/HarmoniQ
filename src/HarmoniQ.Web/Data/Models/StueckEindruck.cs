namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Privater Live-Eindruck eines Users zu einem konkret gespielten Programmpunkt
/// (<see cref="KonzertStueck"/> = Stück + Band + Konzert), Teil des Konzert-Tagebuchs
/// (UX-Spec 4.1). Sterne (1–5) und/oder Notiz. Bewusst <b>getrennt</b> von der öffentlichen
/// Video-<see cref="Bewertung"/> (Aufnahme ≠ Live-Eindruck). Ein Eintrag pro (User, KonzertStück).
/// </summary>
public class StueckEindruck
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KonzertStueckId { get; set; }
    public KonzertStueck KonzertStueck { get; set; } = null!;

    /// <summary>Eingeloggte:r Nutzer:in (AspNet-Identity-Id).</summary>
    public string BenutzerId { get; set; } = null!;
    public ApplicationUser? Benutzer { get; set; }

    /// <summary>Private Bewertung 1–5 Sterne; null, wenn nur eine Notiz erfasst wurde.</summary>
    public int? Sterne { get; set; }

    /// <summary>Private Notiz zu diesem Stück, wie es an diesem Konzert gespielt wurde.</summary>
    public string? Notiz { get; set; }

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime? GeaendertAm { get; set; }
}
