namespace HarmoniQ.Web.Data.Models;

/// <summary>n:m – welche <see cref="Band"/>s bei einem <see cref="Konzert"/> mitwirken.
/// PK = (KonzertId, BandId). Wird beim Verknüpfen eines Videos mit einem Konzert
/// automatisch für die Video-Band angelegt (idempotent).</summary>
public class KonzertBand
{
    public Guid KonzertId { get; set; }
    public Konzert Konzert { get; set; } = null!;

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;

    /// <summary>Platzierung bei Wettbewerbs-Konzerten (1 = Sieger); sonst null.</summary>
    public int? Rang { get; set; }
    /// <summary>Erreichte Punkte (falls auf der Rangliste vorhanden); sonst null.</summary>
    public int? Punkte { get; set; }
}
