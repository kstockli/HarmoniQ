// Minimaler Leaflet-Wrapper für die Lokal-Karte auf der Konzert-Detailseite.
// Zeigt eine OpenStreetMap-Karte mit einem Marker an den übergebenen Koordinaten.
window.lokalKarte = {
    init: function (elId, lat, lng, name) {
        if (!window.L) return;                       // Leaflet noch nicht geladen
        var el = document.getElementById(elId);
        if (!el) return;
        if (el._leaflet_id) { return; }              // schon initialisiert
        var map = L.map(elId, { scrollWheelZoom: false }).setView([lat, lng], 15);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap-Mitwirkende'
        }).addTo(map);
        var marker = L.marker([lat, lng]).addTo(map);
        if (name) { marker.bindPopup(name); }
        // Nach dem Einblenden Grösse neu berechnen (sonst graue Kacheln).
        setTimeout(function () { map.invalidateSize(); }, 200);
    }
};
