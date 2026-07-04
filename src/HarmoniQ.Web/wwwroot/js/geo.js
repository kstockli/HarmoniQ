// Standort-Helfer für den Distanz-Filter: Browser-Geolocation (opt-in) + localStorage-Merker.
// Der Bezugspunkt wird NICHT serverseitig gespeichert (datensparsam).
window.harmoniqGeo = {
    // Fragt den Browser um den aktuellen Standort; gibt [lat, lng] oder null zurück.
    standort: function () {
        return new Promise(function (resolve) {
            if (!navigator.geolocation) { resolve(null); return; }
            navigator.geolocation.getCurrentPosition(
                function (p) { resolve([p.coords.latitude, p.coords.longitude]); },
                function () { resolve(null); },
                { timeout: 8000, maximumAge: 300000 }
            );
        });
    },
    // Stiller Abruf: aktualisiert den Standort NUR, wenn die Berechtigung bereits erteilt ist
    // (kein erneuter Prompt). Gecachter Fix bis 10 min (schnell, ~1 km genügt). Sonst null.
    autoStandort: function () {
        var self = this;
        return new Promise(function (resolve) {
            if (!navigator.geolocation || !navigator.permissions || !navigator.permissions.query) { resolve(null); return; }
            navigator.permissions.query({ name: 'geolocation' }).then(function (status) {
                if (status.state !== 'granted') { resolve(null); return; }
                navigator.geolocation.getCurrentPosition(
                    function (p) { var c = [p.coords.latitude, p.coords.longitude]; self.save(c[0], c[1]); resolve(c); },
                    function () { resolve(null); },
                    { timeout: 8000, maximumAge: 600000, enableHighAccuracy: false }
                );
            }).catch(function () { resolve(null); });
        });
    },
    save: function (lat, lng) {
        try { localStorage.setItem('harmoniq.geo', JSON.stringify([lat, lng])); } catch (e) { }
    },
    load: function () {
        try { var s = localStorage.getItem('harmoniq.geo'); return s ? JSON.parse(s) : null; }
        catch (e) { return null; }
    },
    clear: function () { try { localStorage.removeItem('harmoniq.geo'); } catch (e) { } }
};
