using System.ComponentModel.DataAnnotations.Schema;

namespace HarmoniQ.Web.Data.Models;

public class Stueck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titel { get; set; } = string.Empty;
    public int? Jahr { get; set; }
    public Schwierigkeitsgrad Schwierigkeitsgrad { get; set; } = Schwierigkeitsgrad.Unbekannt;
    public string? Besetzung { get; set; }
    public string? Beschreibung { get; set; }
    public string? OriginalUrl { get; set; }

    public ICollection<Video> Videos { get; set; } = [];
    public ICollection<StueckBeitrag> Beitraege { get; set; } = [];

    /// <summary>Komponist:innen des Stücks (aus den Beiträgen). Setzt geladene Beiträge+Person voraus.</summary>
    [NotMapped]
    public IEnumerable<Person> Komponisten =>
        Beitraege.Where(b => b.Rolle == StueckRolle.Komponist).Select(b => b.Person);

    /// <summary>Anzeigetext der Komponist:innen (Sichtbarkeit berücksichtigt).</summary>
    [NotMapped]
    public string KomponistenText => string.Join(", ", Komponisten.Select(p => p.AnzeigeName));
}
