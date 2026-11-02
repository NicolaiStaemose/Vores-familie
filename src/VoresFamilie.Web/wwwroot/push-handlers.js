// Shared by service-worker.js (development) and service-worker.published.js: shows push messages and opens the app on click.

self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch { data = { body: event.data ? event.data.text() : '' }; }

    const title = data.title || 'Vores familie';
    event.waitUntil(self.registration.showNotification(title, {
        body: data.body || '',
        icon: 'icon-192.png',
        tag: data.tag || 'vores-familie',
        data: { url: data.url || '/min-side' },
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL((event.notification.data && event.notification.data.url) || '/min-side', self.location.origin).href;

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
                await client.focus();
                if ('navigate' in client) { try { await client.navigate(target); } catch { /* cross-document navigation can be refused */ } }
                return;
            }
        }
        await self.clients.openWindow(target);
    })());
});

// The page asks a waiting service worker to take over (the "new version" banner).
self.addEventListener('message', event => {
    if (event.data === 'skip-waiting') self.skipWaiting();
});
