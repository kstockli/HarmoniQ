using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Hintergrund-Dienst, der eingereihte Crawl-Läufe sequenziell ausführt (Spec §4 „IHostedService").
/// Beim Start werden verwaiste „Laufend"-Läufe (durch Neustart/Redeploy abgebrochen) auf
/// <see cref="CrawlLaufStatus.Abgebrochen"/> gesetzt – auf Railway überlebt kein Lauf einen Neustart.
/// </summary>
public class CrawlHostedService(
    CrawlLaufQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<CrawlHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await VerwaisteLaeufeAufraeumenAsync(stoppingToken);

        await foreach (var laufId in queue.LeseAlleAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<CrawlRunner>();
                await runner.AusfuehrenAsync(laufId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawl-Lauf {LaufId} fehlgeschlagen.", laufId);
                await LaufAlsFehlerMarkierenAsync(laufId, ex.Message);
            }
        }
    }

    private async Task VerwaisteLaeufeAufraeumenAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var verwaist = await db.CrawlLaeufe
                .Where(l => l.Status == CrawlLaufStatus.Laufend)
                .ToListAsync(ct);
            foreach (var l in verwaist)
            {
                l.Status = CrawlLaufStatus.Abgebrochen;
                l.EndeAm = DateTime.UtcNow;
                l.Meldung = "Durch Neustart abgebrochen.";
            }
            if (verwaist.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("{Count} verwaiste Crawl-Läufe als abgebrochen markiert.", verwaist.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aufräumen verwaister Läufe fehlgeschlagen.");
        }
    }

    private async Task LaufAlsFehlerMarkierenAsync(Guid laufId, string meldung)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lauf = await db.CrawlLaeufe.FindAsync(laufId);
            if (lauf != null && lauf.Status == CrawlLaufStatus.Laufend)
            {
                lauf.Status = CrawlLaufStatus.Fehler;
                lauf.EndeAm = DateTime.UtcNow;
                lauf.Meldung = meldung;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Konnte Lauf {LaufId} nicht als Fehler markieren.", laufId);
        }
    }
}
