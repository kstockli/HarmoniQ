using System.ComponentModel.DataAnnotations.Schema;

namespace HarmoniQ.Web.Data.Models;

public class Band : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Land { get; set; }
    /// <summary>Haupt-Homepage der Band (weitere Links siehe <see cref="Links"/>).</summary>
    public string? Webseite { get; set; }
    /// <summary>Optionales Band-Logo (kleines Marken-/Vereinszeichen).</summary>
    public string? BildUrl { get; set; }
    /// <summary>Optionales Band-Foto (grösseres Gruppen-/Promobild). Nur verlinkt (nicht selbst gehostet).</summary>
    public string? FotoUrl { get; set; }
    /// <summary>Quellen-/Fotograf:in-Angabe zum Band-Foto (Urheberrecht) – anzeigefertig, z. B.
    /// „Foto: Verein XY / Fotograf:in".</summary>
    public string? FotoAttribution { get; set; }

    public BandKategorie? Kategorie { get; set; }
    public Staerkeklasse? Staerkeklasse { get; set; }
    public int? Gruendungsjahr { get; set; }
    /// <summary>Geschichte/Beschreibung der Band (analog Person.Biografie).</summary>
    public string? Geschichte { get; set; }

    /// <summary>Heimatort/Probelokal der Band als <see cref="Lokal"/>-Referenz (Ortschaft genügt) –
    /// liefert Koordinaten für „Bands in der Nähe" (UX-Spec §4.4). Optional.</summary>
    public Guid? HeimatLokalId { get; set; }
    public Lokal? HeimatLokal { get; set; }

    /// <summary>Admin hat bewusst entschieden, diese Band NICHT (per gefundenem Kontakt) als Band-Admin
    /// einzuladen. Null = noch offen. Steuert die „Einladungs-Vorschläge"-Liste (Phase 2 A).</summary>
    public DateTime? EinladungVerworfenAm { get; set; }
    /// <summary>AspNetUsers-Id des Admins, der die Einladung verworfen hat.</summary>
    public string? EinladungVerworfenVon { get; set; }

    public ICollection<Video> Videos { get; set; } = [];
    public ICollection<BandMitgliedschaft> Mitgliedschaften { get; set; } = [];
    /// <summary>Personen, die dieser Band „folgen" (privat) – UX-Spec 4.2.</summary>
    public ICollection<BandInteresse> Interessenten { get; set; } = [];
    public ICollection<KonzertBand> Konzertteilnahmen { get; set; } = [];
    public ICollection<BandAlias> Aliase { get; set; } = [];
    public ICollection<BandLink> Links { get; set; } = [];

    // ─── Komfort-Properties für Links (nicht in der DB; Links müssen geladen sein) ───
    [NotMapped] public string? Instagram { get => LinkUrl(LinkTyp.Instagram); set => SetzeLink(LinkTyp.Instagram, value); }
    [NotMapped] public string? X { get => LinkUrl(LinkTyp.X); set => SetzeLink(LinkTyp.X, value); }
    [NotMapped] public string? YouTube { get => LinkUrl(LinkTyp.YouTube); set => SetzeLink(LinkTyp.YouTube, value); }
    [NotMapped] public string? Imagefilm { get => LinkUrl(LinkTyp.Imagefilm); set => SetzeLink(LinkTyp.Imagefilm, value); }
    [NotMapped] public string? Facebook { get => LinkUrl(LinkTyp.Facebook); set => SetzeLink(LinkTyp.Facebook, value); }
    [NotMapped] public string? Wikipedia { get => LinkUrl(LinkTyp.Wikipedia); set => SetzeLink(LinkTyp.Wikipedia, value); }
    [NotMapped] public string? EMail { get => LinkUrl(LinkTyp.EMail); set => SetzeLink(LinkTyp.EMail, value); }
    [NotMapped] public string? Mobile { get => LinkUrl(LinkTyp.Mobile); set => SetzeLink(LinkTyp.Mobile, value); }

    private string? LinkUrl(LinkTyp typ)
    {
        var url = Links.FirstOrDefault(l => l.Typ == typ)?.Url?.Trim();
        if (string.IsNullOrWhiteSpace(url)) return null;
        // Nur echte Links liefern – Import-Altlasten (z. B. "|null", "null") ausblenden, damit keine
        // toten Buttons entstehen und ein erneutes Speichern das Feld bereinigt.
        bool gueltig = typ switch
        {
            LinkTyp.EMail  => url.Contains('@') && !url.Contains(' '),
            LinkTyp.Mobile => url.Any(char.IsDigit),
            _              => url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                           || (url.Contains('.') && !url.Contains(' ')),   // Domain ohne Schema zulassen
        };
        return gueltig ? url : null;
    }

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
