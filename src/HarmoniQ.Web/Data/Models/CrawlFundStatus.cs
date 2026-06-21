namespace HarmoniQ.Web.Data.Models;

/// <summary>Bearbeitungsstand eines <see cref="CrawlFund"/> in der Review-Queue.</summary>
public enum CrawlFundStatus
{
    Offen = 0,
    Uebernommen = 1,
    Verworfen = 2
}
