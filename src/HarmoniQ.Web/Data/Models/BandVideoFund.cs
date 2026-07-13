namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Kandidat aus der YouTube-Suche pro Band (Band-Admin, on-demand). Der Crawler sucht je Band über
/// den Bandnamen, lässt das LLM aus dem Videotitel Stück + Komponist:in vorschlagen und legt jeden
/// Treffer hier als <see cref="CrawlFundStatus.Offen"/> ab. Der/die Band-Admin entscheidet dann
/// (Felder editierbar) Übernehmen/Ablehnen. Der gespeicherte Status sorgt dafür, dass ein erneuter
/// Suchlauf entschiedene Videos nicht wieder anzeigt („nur Neueres"). Isoliert vom Kernmodell –
/// einziger FK-Berührungspunkt ist die Ziel-<see cref="Band"/>.
/// </summary>
public class BandVideoFund : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BandId { get; set; }
    public Band? Band { get; set; }

    /// <summary>YouTube-Video-ID (11-stellig). Dedup-Schlüssel je Band.</summary>
    public string ExternId { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    /// <summary>YouTube-Kanalname des Treffers (Kontext für die Review).</summary>
    public string? KanalName { get; set; }

    /// <summary>Vom LLM aus dem Titel erkanntes Stück (Vorschlag, in der Review editierbar).</summary>
    public string? StueckVorschlag { get; set; }

    /// <summary>Vom LLM erkannte:r Komponist:in (Vorschlag, in der Review editierbar).</summary>
    public string? KomponistVorschlag { get; set; }

    public CrawlFundStatus Status { get; set; } = CrawlFundStatus.Offen;

    public DateTime GefundenAm { get; set; } = DateTime.UtcNow;
    public DateTime? EntschiedenAm { get; set; }

    /// <summary>Bei Übernahme: das daraus erzeugte Video.</summary>
    public Guid? ErgebnisVideoId { get; set; }
}
