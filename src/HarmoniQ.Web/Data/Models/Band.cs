namespace HarmoniQ.Web.Data.Models;

public class Band
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Land { get; set; }
    public string? Webseite { get; set; }

    public ICollection<Video> Videos { get; set; } = [];
}
