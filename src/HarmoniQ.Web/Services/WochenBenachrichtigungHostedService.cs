using HarmoniQ.Web.Data;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Löst den wöchentlichen Wochenüberblick (UX-Spec 4.2) aus. Prüft stündlich; sendet einmal pro Woche
/// am konfigurierten Wochentag/zur Stunde (Standard: Sonntag 18 Uhr Serverzeit). Doppelversand ist
/// unkritisch: <see cref="WochenBenachrichtigung"/> protokolliert versendete Bausteine, ein zweiter
/// Lauf fände nichts Neues → keine zweite Mail.
/// </summary>
public class WochenBenachrichtigungHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<WochenBenachrichtigungHostedService> logger) : BackgroundService
{
    private readonly DayOfWeek _tag = Enum.TryParse<DayOfWeek>(config["Benachrichtigung:Wochentag"], out var t)
        ? t : DayOfWeek.Sunday;
    private readonly int _stunde = int.TryParse(config["Benachrichtigung:Stunde"], out var h) ? h : 18;
    // Vor diesem Zeitpunkt wird NIE gesendet (Steuerung des ersten Versands). Leer = keine Sperre.
    private readonly DateTime? _startAb = DateTime.TryParse(config["Benachrichtigung:StartAb"], out var s) ? s : null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            do
            {
                var jetzt = DateTime.Now;
                if (_startAb is { } start && jetzt < start) continue;   // Erststart-Sperre
                if (jetzt.DayOfWeek == _tag && jetzt.Hour == _stunde)
                    await LaufAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* Shutdown */ }
    }

    private async Task LaufAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IBenachrichtigungsMail>();
            var basis = config["Seo:BasisUrl"] ?? "https://harmoniq.q-no.ch";

            var n = await WochenBenachrichtigung.VersendeAlleAsync(db, mail, config, basis, ct);
            logger.LogInformation("Wochenüberblick: an {Count} Konten ausgelöst.", n);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Wochenüberblick-Versand fehlgeschlagen.");
        }
    }
}
