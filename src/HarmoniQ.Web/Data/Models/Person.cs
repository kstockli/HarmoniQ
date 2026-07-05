using System.ComponentModel.DataAnnotations.Schema;

namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Eine Person im Musik-Kontext (ersetzt langfristig „Komponist"). Kann mehrere Rollen haben
/// (Komponist:in / Dirigent:in / Musikant:in). Optional mit einem Benutzerkonto verknüpft.
/// </summary>
public class Person : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Datenschutz-Stufe der öffentlichen Anzeige.</summary>
    public Sichtbarkeit Sichtbarkeit { get; set; } = Sichtbarkeit.Oeffentlich;

    public string? Biografie { get; set; }
    public string? BildUrl { get; set; }
    public int? Geburtsjahr { get; set; }

    /// <summary>Optionale, eindeutige „das bin ich"-Verknüpfung zum eingeloggten Konto.</summary>
    public string? BenutzerId { get; set; }
    public ApplicationUser? Benutzer { get; set; }

    // ─── Privater Standort-Bezug für den „in deiner Nähe"-Digest (UX-Spec 4.2, Trigger F) ───
    // Opt-in, nur für die eigene Person sichtbar/nutzbar. Koordinaten bewusst VERGRÖBERT (~1 km)
    // gespeichert (Datensparsamkeit); dienen nur der serverseitigen Distanzberechnung im Wochen-Job.
    /// <summary>Vergröberte Heim-Breite (opt-in); null = kein Nähe-Bezug.</summary>
    public double? StandortLat { get; set; }
    /// <summary>Vergröberte Heim-Länge (opt-in).</summary>
    public double? StandortLng { get; set; }
    /// <summary>Optionale Heimat-PLZ (Anzeige/Neu-Geocoding), z. B. wenn kein GPS genutzt wird.</summary>
    public string? HeimatPlz { get; set; }

    public ICollection<PersonRolle> Rollen { get; set; } = [];
    public ICollection<PersonLink> Links { get; set; } = [];
    public ICollection<PersonInstrument> Instrumente { get; set; } = [];
    public ICollection<StueckBeitrag> StueckBeitraege { get; set; } = [];
    public ICollection<VideoMitwirkung> Mitwirkungen { get; set; } = [];
    public ICollection<BandMitgliedschaft> Bandmitgliedschaften { get; set; } = [];
    /// <summary>Bands, denen diese Person „folgt" (privat, kein Roster-Eintrag) – UX-Spec 4.2.</summary>
    public ICollection<BandInteresse> GefolgteBands { get; set; } = [];
    /// <summary>Alternative Namen derselben Person (für Find-or-create &amp; Merge).</summary>
    public ICollection<PersonAlias> Aliase { get; set; } = [];

    // ─── Read-only / Komfort-Properties (nicht in der DB gespeichert) ───────────

    /// <summary>Öffentlich anzuzeigender Name je nach Sichtbarkeit.</summary>
    [NotMapped]
    public string AnzeigeName => Sichtbarkeit switch
    {
        Sichtbarkeit.Oeffentlich => Name,
        Sichtbarkeit.NurInitialen => Initialen(Name),
        _ => "?"
    };

    /// <summary>Link-Komfort-Properties: lesen/setzen den passenden <see cref="PersonLink"/>
    /// in <see cref="Links"/> (Voraussetzung: Links sind geladen). Setzen auf null/leer entfernt ihn.</summary>
    [NotMapped] public string? Webseite { get => LinkUrl(LinkTyp.Webseite); set => SetzeLink(LinkTyp.Webseite, value); }
    [NotMapped] public string? Instagram { get => LinkUrl(LinkTyp.Instagram); set => SetzeLink(LinkTyp.Instagram, value); }
    [NotMapped] public string? X { get => LinkUrl(LinkTyp.X); set => SetzeLink(LinkTyp.X, value); }
    [NotMapped] public string? Facebook { get => LinkUrl(LinkTyp.Facebook); set => SetzeLink(LinkTyp.Facebook, value); }
    [NotMapped] public string? YouTube { get => LinkUrl(LinkTyp.YouTube); set => SetzeLink(LinkTyp.YouTube, value); }
    [NotMapped] public string? EMail { get => LinkUrl(LinkTyp.EMail); set => SetzeLink(LinkTyp.EMail, value); }
    [NotMapped] public string? Mobile { get => LinkUrl(LinkTyp.Mobile); set => SetzeLink(LinkTyp.Mobile, value); }
    [NotMapped] public string? Wikipedia { get => LinkUrl(LinkTyp.Wikipedia); set => SetzeLink(LinkTyp.Wikipedia, value); }

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
            Links.Add(new PersonLink { Typ = typ, Url = url.Trim(), PersonId = Id });
        }
    }

    private static string Initialen(string name)
    {
        var teile = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length == 0) return "?";
        return string.Join(" ", teile.Select(t => char.ToUpperInvariant(t[0]) + "."));
    }
}
