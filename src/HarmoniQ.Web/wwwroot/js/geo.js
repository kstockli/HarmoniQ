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

// Kleine UI-Helfer: Filter je Tab merken (sessionStorage, überlebt „in Detail → zurück")
// und zu einem Element scrollen (Smartphone: nach Enter zum ersten Resultat).
window.harmoniqUi = {
    get: function (k) { try { return sessionStorage.getItem(k); } catch (e) { return null; } },
    set: function (k, v) { try { sessionStorage.setItem(k, v); } catch (e) { } },
    scrollTo: function (sel) {
        try { var el = document.querySelector(sel); if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch (e) { }
    },
    // Video-Wraps initialisieren: eigene Bedienung (Ton/Vollbild/Klick=Play-Pause) statt nativer Controls.
    // Startet stumm (Autoplay nur muted erlaubt); der „Ton"-Knopf macht das Entstummen auffindbar.
    initVideos: function () {
        try {
            document.querySelectorAll('.hq-video-wrap').forEach(function (wrap) {
                var v = wrap.querySelector('video');
                if (!v || wrap.dataset.hqInit) return;
                wrap.dataset.hqInit = '1';
                var sound = wrap.querySelector('.hq-sound');
                var fs = wrap.querySelector('.hq-fs');
                var speed = wrap.querySelector('.hq-speed');
                // Abspielgeschwindigkeit durchschalten (Video ist etwas schnell). Oben rechts, damit die
                // eingebrannten Untertitel unten frei bleiben.
                var stufen = [1, 0.75, 0.5], si = 0;
                if (speed) speed.addEventListener('click', function (e) {
                    e.stopPropagation();
                    si = (si + 1) % stufen.length;
                    v.playbackRate = stufen[si];
                    speed.textContent = stufen[si] + '×';
                });
                function refresh() { if (sound) sound.textContent = v.muted ? '🔊 Ton' : '🔇'; }
                v.muted = true; v.defaultMuted = true;
                if (v.dataset.autoplay === '1') { var p = v.play(); if (p && p.catch) p.catch(function () { }); }
                if (sound) sound.addEventListener('click', function (e) {
                    e.stopPropagation(); v.muted = !v.muted;
                    if (!v.muted && v.paused) { var q = v.play(); if (q && q.catch) q.catch(function () { }); }
                    refresh();
                });
                if (fs) fs.addEventListener('click', function (e) {
                    e.stopPropagation();
                    if (wrap.requestFullscreen) wrap.requestFullscreen();
                    else if (v.webkitEnterFullscreen) v.webkitEnterFullscreen();
                });
                v.addEventListener('click', function () {
                    if (v.paused) { var q = v.play(); if (q && q.catch) q.catch(function () { }); } else v.pause();
                });
                v.addEventListener('volumechange', refresh);
                refresh();
            });
        } catch (e) { }
    }
};

// Scrollt ein Element (per id) sanft in den sichtbaren Bereich (z. B. „Zu den Videos").
window.harmoniqScroll = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};
