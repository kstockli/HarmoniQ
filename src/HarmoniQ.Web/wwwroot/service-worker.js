// Minimaler Service Worker – nur für die PWA-Installierbarkeit.
// HarmoniQ läuft als Blazor Server und benötigt eine Live-Verbindung, daher KEIN
// Offline-Caching der App. Der Fetch-Handler ist absichtlich ein Pass-through.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* Standard-Netzwerkverhalten beibehalten */ });
