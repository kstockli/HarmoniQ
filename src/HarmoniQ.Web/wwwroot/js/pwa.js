// PWA-Install-Unterstützung. Das beforeinstallprompt-Event wird bereits früh im <head>
// (App.razor) in window.__pwaPrompt abgefangen, weil es teils vor dem Blazor-Start feuert.
// Dieses Modul liest/prompt darüber und meldet Zustandsänderungen an die Blazor-Komponente.
let dotnetRef = null;

export function init(ref) {
    dotnetRef = ref;

    // Falls das Event später (nach Blazor-Start) feuert, ebenfalls reagieren.
    window.addEventListener('beforeinstallprompt', (e) => {
        e.preventDefault();
        window.__pwaPrompt = e;
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnInstallable');
    });
    window.addEventListener('appinstalled', () => {
        window.__pwaPrompt = null;
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnInstalled');
    });

    return getState();
}

export function getState() {
    const standalone =
        window.matchMedia('(display-mode: standalone)').matches ||
        window.navigator.standalone === true;
    const ua = navigator.userAgent || '';
    const isIOS = /iphone|ipad|ipod/i.test(ua) ||
        (/Macintosh/.test(ua) && 'ontouchend' in document);
    const isAndroid = /android/i.test(ua);
    return { standalone, isIOS, isAndroid, canPrompt: !!window.__pwaPrompt };
}

export async function install() {
    const prompt = window.__pwaPrompt;
    if (!prompt) return 'unavailable';
    prompt.prompt();
    const choice = await prompt.userChoice;
    window.__pwaPrompt = null;
    return choice && choice.outcome ? choice.outcome : 'dismissed';
}

export function getDismissed() {
    try { return localStorage.getItem('pwa-install-dismissed') === '1'; } catch { return false; }
}

export function setDismissed() {
    try { localStorage.setItem('pwa-install-dismissed', '1'); } catch { }
}
