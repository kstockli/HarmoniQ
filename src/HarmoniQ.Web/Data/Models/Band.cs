using System.ComponentModel.DataAnnotations.Schema;

namespace HarmoniQ.Web.Data.Models;

public class Band : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Land { get; set; }
    /// <summary>Haupt-Homepage der Band (weitere Links siehe <see cref="Links"/>).</summary>
    public string? Webseite { get; set; }
    /// <summary>Optionales Band-Logo/Foto.</summary>
    public string? BildUrl { get; set; }

    public BandKategorie? Kategorie { get; set; }
    public Staerkeklasse? Staerkeklasse { get; set; }
    public int? Gruendungsjahr { get; set; }
    /// <summary>Geschichte/Beschreibung der Band (analog Person.Biografie).</summary>
    public string? Geschichte { get; set; }

    public ICollection<Video> Videos { get; set; } = [];
    public ICollection<BandMitgliedschaft> Mitgliedschaften { get; set; } = [];
    public ICollection<KonzertBand> Konzertteilnahmen { get; set; } = [];
    public ICollection<BandAlias> Aliase { get; set; } = [];
    public ICollection<BandLink> Links { get; set; } = [];

    // ─── Komfort-Properties für Links (nicht in der DB; Links müssen geladen sein) ───
    [NotMapped] public string? Instagram { get => LinkUrl(LinkTyp.Instagram); set => SetzeLink(LinkTyp.Instagram, value); }
    [NotMapped] public string? X { get => LinkUrl(LinkTyp.X); set => SetzeLink(LinkTyp.X, value); }
    [NotMapped] public string? YouTube { get => LinkUrl(LinkTyp.YouTube); set => SetzeLink(LinkTyp.YouTube, value); }
    [NotMapped] public string? Facebook { get => LinkUrl(LinkTyp.Facebook); set => SetzeLink(LinkTyp.Facebook, value); }
    [NotMapped] public string? Wikipedia { get => LinkUrl(LinkTyp.Wikipedia); set => SetzeLink(LinkTyp.Wikipedia, value); }
    [NotMapped] public string? EMail { get => LinkUrl(LinkTyp.EMail); set => SetzeLink(LinkTyp.EMail, value); }
    [NotMapped] public string? Mobile { get => LinkUrl(LinkTyp.Mobile); set => SetzeLink(LinkTyp.Mobile, value); }

    private string? LinkUrl(LinkTyp typ) => Links.FirstOrDefault(l => l.Typ == typ)?.Url;

    private void SetzeLink(LinkTyp typ, string? url)
    {
        var vorhanden = Links.FirstOrDefault(l => l.Typ == typ);
        if (string.IsNullOrWhiteSpace(url))
        {
            if (vorhanden != null) Links.Remove(vorhanden);
        }
        else if (vorhanden != null)
        {
            vorhanden.Url = url.Trim();
        }
        else
        {
            Links.Add(new BandLink { Typ = typ, Url = url.Trim(), BandId = Id });
        }
    }
}
