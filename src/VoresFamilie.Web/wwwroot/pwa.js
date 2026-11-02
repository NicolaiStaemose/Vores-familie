// Glue between the browser's PWA features and Blazor (installing, updates, online/offline, push).
window.pwa = (function () {
    let dotnet = null;
    let installPrompt = null;
    let refreshing = false;

    const notify = (method, ...args) => { if (dotnet) dotnet.invokeMethodAsync(method, ...args).catch(() => { }); };

    function watchForUpdates(registration) {
        const announce = () => notify('OnUpdateReady');
        if (registration.waiting && navigator.serviceWorker.controller) announce();
        registration.addEventListener('updatefound', () => {
            const worker = registration.installing;
            if (!worker) return;
            worker.addEventListener('statechange', () => {
                if (worker.state === 'installed' && navigator.serviceWorker.controller) announce();
            });
        });
    }

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        notify('OnInstallAvailable', true);
    });
    window.addEventListener('appinstalled', () => { installPrompt = null; notify('OnInstallAvailable', false); });
    window.addEventListener('online', () => notify('OnOnlineChanged', true));
    window.addEventListener('offline', () => notify('OnOnlineChanged', false));

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (refreshing) return;
            refreshing = true;
            window.location.reload();
        });
    }

    async function registration() {
        if (!('serviceWorker' in navigator)) return null;
        return (await navigator.serviceWorker.getRegistration()) || (await navigator.serviceWorker.ready);
    }

    function toBase64(buffer) {
        if (!buffer) return '';
        const bytes = new Uint8Array(buffer);
        let text = '';
        for (const b of bytes) text += String.fromCharCode(b);
        return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function fromBase64(value) {
        const padded = (value + '='.repeat((4 - value.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(padded);
        return Uint8Array.from(raw, c => c.charCodeAt(0));
    }

    return {
        init: async function (ref) {
            dotnet = ref;
            const reg = await registration();
            if (reg) watchForUpdates(reg);
            return {
                online: navigator.onLine,
                canInstall: installPrompt !== null,
                standalone: window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true,
            };
        },

        isOnline: () => navigator.onLine,

        // Removes every localStorage key that starts with the prefix (the app's saved offline copy).
        clearStorage: function (prefix) {
            try {
                Object.keys(localStorage).filter(k => k.startsWith(prefix)).forEach(k => localStorage.removeItem(k));
            } catch { /* storage blocked */ }
        },

        install: async function () {
            if (!installPrompt) return false;
            installPrompt.prompt();
            const choice = await installPrompt.userChoice;
            installPrompt = null;
            notify('OnInstallAvailable', false);
            return choice.outcome === 'accepted';
        },

        applyUpdate: async function () {
            const reg = await registration();
            if (reg && reg.waiting) reg.waiting.postMessage('skip-waiting');
            else window.location.reload();
        },

        // ---- Push ----

        pushSupported: () => 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window,

        pushPermission: () => ('Notification' in window ? Notification.permission : 'denied'),

        // The endpoint of this phone's subscription, or null when it is not subscribed.
        pushEndpoint: async function () {
            const reg = await registration();
            const sub = reg ? await reg.pushManager.getSubscription() : null;
            return sub ? sub.endpoint : null;
        },

        // Asks for permission if needed and subscribes. Returns { endpoint, p256dh, auth } or null when permission was refused.
        pushSubscribe: async function (publicKey) {
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') return null;
            const reg = await registration();
            let sub = await reg.pushManager.getSubscription();
            if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: fromBase64(publicKey) });
            return { endpoint: sub.endpoint, p256dh: toBase64(sub.getKey('p256dh')), auth: toBase64(sub.getKey('auth')) };
        },

        // Cancels this phone's subscription in the browser. Returns the endpoint so the server can forget it too.
        pushUnsubscribe: async function () {
            const reg = await registration();
            const sub = reg ? await reg.pushManager.getSubscription() : null;
            if (!sub) return null;
            const endpoint = sub.endpoint;
            await sub.unsubscribe();
            return endpoint;
        },
    };
})();
