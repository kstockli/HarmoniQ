// Web-Push-Anmeldung im Browser (PWA-Push, UX-Spec 4.2).
// Wird von der Seite „Benachrichtigungen" per JS-Interop aufgerufen.
(function () {
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const out = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    async function registration() {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        return await navigator.serviceWorker.ready;
    }

    async function post(url, sub) {
        const key = sub.getKey ? sub : null;
        const body = key ? {
            endpoint: sub.endpoint,
            p256dh: btoa(String.fromCharCode.apply(null, new Uint8Array(sub.getKey('p256dh')))),
            auth: btoa(String.fromCharCode.apply(null, new Uint8Array(sub.getKey('auth'))))
        } : { endpoint: sub, p256dh: '', auth: '' };
        await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
    }

    window.harmoniqPush = {
        // 'unsupported' | 'denied' | 'default' | 'subscribed'
        status: async function () {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) return 'unsupported';
            if (Notification.permission === 'denied') return 'denied';
            const reg = await registration();
            if (!reg) return 'unsupported';
            const sub = await reg.pushManager.getSubscription();
            if (sub) return 'subscribed';
            return Notification.permission === 'granted' ? 'default' : 'default';
        },

        aktivieren: async function (publicKey) {
            const reg = await registration();
            if (!reg) return 'unsupported';
            const perm = await Notification.requestPermission();
            if (perm !== 'granted') return 'denied';
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(publicKey)
                });
            }
            await post('/api/push/subscribe', sub);
            return 'subscribed';
        },

        deaktivieren: async function () {
            const reg = await registration();
            if (!reg) return 'unsupported';
            const sub = await reg.pushManager.getSubscription();
            if (sub) {
                await post('/api/push/unsubscribe', sub.endpoint);
                await sub.unsubscribe();
            }
            return 'default';
        }
    };
})();
