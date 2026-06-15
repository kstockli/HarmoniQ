namespace HarmoniQ.Web.Data.Models;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StueckId { get; set; }
    public Guid? BandId { get; set; }
    public string YouTubeVideoId { get; set; } = string.Empty;
    public string Titel { get; set; } = string.Empty;
    public DateOnly? AufnahmeDatum { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Ausstehend;
    public string? VorgeschlagenVonId { get; set; }

    public Stueck Stueck { get; set; } = null!;
    public Band? Band { get; set; }
    public ApplicationUser? VorgeschlagenVon { get; set; }
    public ICollection<Bewertung> Bewertungen { get; set; } = [];
    public ICollection<VideoMitwirkung> Mitwirkungen { get; set; } = [];
}
