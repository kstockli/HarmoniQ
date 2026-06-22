using System.Threading.Channels;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// In-Memory-Warteschlange für Crawl-Läufe (Spec §4 „Betrieb: On-demand"). Der Admin reiht eine
/// <c>CrawlLauf</c>-Id ein; der <see cref="CrawlHostedService"/> arbeitet sie sequenziell ab
/// (max. 1 Lauf gleichzeitig). Singleton. Auf Railway läuft genau eine Instanz – das genügt.
/// </summary>
public class CrawlLaufQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Einreihen(Guid laufId) => _channel.Writer.TryWrite(laufId);

    public IAsyncEnumerable<Guid> LeseAlleAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
