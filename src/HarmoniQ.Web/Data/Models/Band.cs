namespace HarmoniQ.Web.Data.Models;

public class Band
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Land { get; set; }
    public string? Webseite { get; set; }
    /// <summary>Optionales Band-Logo/Foto.</summary>
    public string? BildUrl { get; set; }

    public ICollection<Video> Videos { get; set; } = [];
    public ICollection<BandMitgliedschaft> Mitgliedschaften { get; set; } = [];
    public ICollection<KonzertBand> Konzertteilnahmen { get; set; } = [];
}
