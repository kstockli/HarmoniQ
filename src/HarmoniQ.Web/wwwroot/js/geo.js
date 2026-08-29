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
                var play = wrap.querySelector('.hq-play');
                // Zentraler Play-Knopf: nur sichtbar, solange das Video pausiert ist. Wichtig für
                // Eingeloggte (kein Autoplay – Datenmenge/Performance), damit ein Start-Knopf da ist.
                function refreshPlay() { if (play) play.style.display = v.paused ? '' : 'none'; }
                // Abspielgeschwindigkeit durchschalten (Video ist etwas schnell). Oben rechts, damit die
                // eingebrannten Untertitel unten frei bleiben.
                var stufen = [1, 0.75, 0.5], si = 0;
                if (speed) speed.addEventListener('click', function (e) {
                    e.stopPropagation();
                    si = (si + 1) % stufen.length;
                    v.playbackRate = stufen[si];
                    speed.textContent = stufen[si] + '×';
                });
                // Icon-only: Symbol = AKTUELLER Zustand (🔇 jetzt stumm / 🔊 Ton läuft).
                // Beschriftung nur als Hover-Tooltip (title) + aria-label.
                function refresh() {
                    if (!sound) return;
                    sound.textContent = v.muted ? '🔇' : '🔊';
                    var lbl = v.muted ? 'Ton einschalten' : 'Ton ausschalten';
                    sound.setAttribute('aria-label', lbl);
                    sound.setAttribute('title', lbl);
                }
                if (v.dataset.autoplay === '1') {
                    // Bevorzugt MIT Ton starten. Browser blocken Autoplay-mit-Ton meist bis zur ersten
                    // Interaktion → dann stumm weiterlaufen (Nutzer entstummt per Klick/Hinweis).
                    v.muted = false; v.defaultMuted = false;
                    var p = v.play();
                    if (p && p.catch) p.catch(function () {
                        v.muted = true; var q = v.play(); if (q && q.catch) q.catch(function () { }); refresh();
                    });
                } else {
                    // Eingeloggte: NICHT automatisch laden/abspielen (Datenmenge/Performance) – Play-Knopf zeigen.
                    v.muted = true; v.defaultMuted = true;
                }
                if (play) play.addEventListener('click', function (e) {
                    e.stopPropagation();
                    v.muted = false;   // manueller Start → mit Ton (Nutzer-Interaktion erlaubt das)
                    var q = v.play(); if (q && q.catch) q.catch(function () { });
                    refresh(); refreshPlay();
                });
                v.addEventListener('play', refreshPlay);
                v.addEventListener('pause', refreshPlay);
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

                // Selbst-ausblendender „Für Ton tippen"-Hinweis bei stummem Autoplay.
                var hint = wrap.querySelector('.hq-hint');
                if (hint && v.dataset.autoplay === '1') {
                    var hideHint = function () {
                        hint.style.opacity = '0';
                        setTimeout(function () { hint.style.display = 'none'; }, 400);
                    };
                    hint.style.display = '';
                    void hint.offsetWidth;   // Reflow → Einblend-Transition greift
                    hint.style.opacity = '1';
                    hint.addEventListener('click', function (e) {
                        e.stopPropagation();
                        v.muted = false;
                        if (v.paused) { var q = v.play(); if (q && q.catch) q.catch(function () { }); }
                        refresh(); hideHint();
                    });
                    v.addEventListener('volumechange', function () { if (!v.muted) hideHint(); });
                    setTimeout(hideHint, 4500);
                }

                refresh();
                refreshPlay();
            });
        } catch (e) { }
    }
};

// Scrollt ein Element (per id) sanft in den sichtbaren Bereich (z. B. „Zu den Videos").
window.harmoniqScroll = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};
