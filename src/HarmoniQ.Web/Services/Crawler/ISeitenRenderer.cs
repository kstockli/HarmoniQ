namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Rendert eine Seite mit ausgeführtem JavaScript (für SPA/Wix-Seiten, Spec §4.1/C2) und gibt das
/// resultierende HTML zurück. <c>null</c> = nicht verfügbar/fehlgeschlagen → Aufrufer fällt auf
/// einfachen HTTP-Abruf zurück.
/// </summary>
public interface ISeitenRenderer
{
    Task<string?> RenderAsync(string url, CancellationToken ct = default);
}
