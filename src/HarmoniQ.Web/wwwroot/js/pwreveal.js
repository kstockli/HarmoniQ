// Ergänzt jedes Passwort-Feld um einen "Anzeigen"-Schalter (Auge), der den eingetippten
// Text sichtbar macht. Funktioniert auch auf den statisch gerenderten Account-Seiten,
// da rein DOM-/JS-basiert. Läuft beim ersten Laden und nach Blazor-Enhanced-Navigationen.
(function () {
    function addToggle(inp) {
        if (inp.dataset.pwreveal) return;
        inp.dataset.pwreveal = '1';

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.tabIndex = -1;
        btn.setAttribute('aria-label', 'Passwort anzeigen');
        btn.textContent = '👁';
        btn.style.cssText =
            'position:absolute;right:.5rem;top:50%;transform:translateY(-50%);' +
            'background:none;border:none;cursor:pointer;font-size:1.05rem;opacity:.55;line-height:1;padding:0;z-index:5;';

        // Eltern-Element relativ positionieren und Platz rechts schaffen.
        var host = inp.parentElement || inp;
        var cs = window.getComputedStyle(host);
        if (cs.position === 'static') host.style.position = 'relative';
        inp.style.paddingRight = '2.4rem';

        btn.addEventListener('click', function () {
            var show = inp.type === 'password';
            inp.type = show ? 'text' : 'password';
            btn.style.opacity = show ? '1' : '.55';
            btn.setAttribute('aria-label', show ? 'Passwort verbergen' : 'Passwort anzeigen');
        });

        inp.insertAdjacentElement('afterend', btn);
    }

    function enhance() {
        document.querySelectorAll('input[type=password]').forEach(addToggle);
    }

    document.addEventListener('DOMContentLoaded', enhance);
    document.addEventListener('enhancedload', enhance);   // Blazor enhanced navigation
    setTimeout(enhance, 300);
})();
