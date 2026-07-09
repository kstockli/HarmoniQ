namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Band-skopierte Admin-Rolle (UX-Spec Block 5): ein Konto darf eine <see cref="Band"/> pflegen
/// (Stammdaten inkl. Heimatort, Mitglieder, später Konzerte) und Claims sichtbarer Rollen bestätigen.
/// Bewusst getrennt von <see cref="BandMitgliedschaft"/> (Präsident:in zu SEIN heißt nicht, die App
/// verwalten zu dürfen). Audit-Felder = ernannt-am/-von. Eindeutig pro (BenutzerId, BandId).
/// </summary>
public class BandAdministrator : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>AspNetUsers-Id des berechtigten Kontos.</summary>
    public string BenutzerId { get; set; } = string.Empty;

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;
}
