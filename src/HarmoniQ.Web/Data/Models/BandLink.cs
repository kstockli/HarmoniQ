namespace HarmoniQ.Web.Data.Models;

/// <summary>Ein Link einer Band (Instagram, X, YouTube-Kanal, E-Mail …) – analog zu
/// <see cref="PersonLink"/>. Die Haupt-Homepage bleibt in <see cref="Band.Webseite"/>.</summary>
public class BandLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public LinkTyp Typ { get; set; } = LinkTyp.Webseite;
}
