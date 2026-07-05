// Minimaler Service Worker – nur für die PWA-Installierbarkeit.
// HarmoniQ läuft als Blazor Server und benötigt eine Live-Verbindung, daher KEIN
// Offline-Caching der App. Der Fetch-Handler ist absichtlich ein Pass-through.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* Standard-Netzwerkverhalten beibehalten */ });

// Web-Push (Wiederkehr-Schleife, UX-Spec 4.2): eingehende Push-Nachricht als Notification anzeigen.
self.addEventListener('push', (event) => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = {}; }
    const title = data.title || 'HarmoniQ';
    const options = {
        body: data.body || '',
        data: { url: data.url || '/' },
        icon: '/icon-192.png',
        badge: '/icon-192.png'
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

// Klick auf die Notification: bestehendes Tab fokussieren oder die Ziel-URL öffnen.
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const url = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil((async () => {
        const all = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const c of all) {
            if ('focus' in c) { c.navigate(url); return c.focus(); }
        }
        if (clients.openWindow) return clients.openWindow(url);
    })());
});
