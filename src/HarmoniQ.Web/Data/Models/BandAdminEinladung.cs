namespace HarmoniQ.Web.Data.Models;

public enum BandAdminEinladungStatus { Offen = 0, Angenommen = 1, Storniert = 2 }

/// <summary>
/// Einladung, eine <see cref="Band"/> als Band-Admin zu verwalten – auch für Personen, die
/// (noch) kein Konto haben (UX-Spec §5.3.2). Ein Einmal-<see cref="Token"/> wird per E-Mail
/// verschickt; beim Annehmen (nach optionaler Registrierung) entsteht ein
/// <see cref="BandAdministrator"/>. Verallgemeinert das „Ernennen per E-Mail" (bestehendes Konto).
/// </summary>
public class BandAdminEinladung : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Zufälliger Einmal-Token (im Einladungs-Link).</summary>
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public string Email { get; set; } = string.Empty;

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;

    /// <summary>AspNetUsers-Id der einladenden Person (Admin/Band-Admin).</summary>
    public string? EingeladenVon { get; set; }

    public DateTime AblaufAm { get; set; }
    public BandAdminEinladungStatus Status { get; set; } = BandAdminEinladungStatus.Offen;

    public bool IstOffenUndGueltig => Status == BandAdminEinladungStatus.Offen && AblaufAm > DateTime.UtcNow;
}
