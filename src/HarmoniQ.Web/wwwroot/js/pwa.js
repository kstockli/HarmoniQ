// PWA-Install-Unterstützung. Fängt das Chrome/Android-Ereignis `beforeinstallprompt`
// ab und meldet Installierbarkeit/Zustand an die Blazor-Komponente.
let deferredPrompt = null;
let dotnetRef = null;

export function init(ref) {
    dotnetRef = ref;

    window.addEventListener('beforeinstallprompt', (e) => {
        e.preventDefault();
        deferredPrompt = e;
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnInstallable');
    });

    window.addEventListener('appinstalled', () => {
        deferredPrompt = null;
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnInstalled');
    });

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('service-worker.js').catch(() => { });
    }

    return getState();
}

export function getState() {
    const standalone =
        window.matchMedia('(display-mode: standalone)').matches ||
        window.navigator.standalone === true;
    const ua = navigator.userAgent || '';
    const isIOS = /iphone|ipad|ipod/i.test(ua) ||
        // iPadOS meldet sich teils als Mac mit Touch
        (/Macintosh/.test(ua) && 'ontouchend' in document);
    const isAndroid = /android/i.test(ua);
    return { standalone, isIOS, isAndroid, canPrompt: deferredPrompt !== null };
}

export async function install() {
    if (!deferredPrompt) return 'unavailable';
    deferredPrompt.prompt();
    const choice = await deferredPrompt.userChoice;
    deferredPrompt = null;
    return choice && choice.outcome ? choice.outcome : 'dismissed';
}

export function getDismissed() {
    try { return localStorage.getItem('pwa-install-dismissed') === '1'; } catch { return false; }
}

export function setDismissed() {
    try { localStorage.setItem('pwa-install-dismissed', '1'); } catch { }
}
