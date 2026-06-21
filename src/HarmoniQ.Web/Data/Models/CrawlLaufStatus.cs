namespace HarmoniQ.Web.Data.Models;

/// <summary>Status eines <see cref="CrawlLauf"/>.</summary>
public enum CrawlLaufStatus
{
    Laufend = 0,
    Fertig = 1,
    Fehler = 2,
    Abgebrochen = 3
}
