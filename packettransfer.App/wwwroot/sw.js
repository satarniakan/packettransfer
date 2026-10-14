/* packettransfer.App/wwwroot/sw.js — service worker سامانه (PWA + وب‌پوش).
   - کش سبک فایل‌های استاتیک پوسته برای نصب‌پذیری PWA و بارگذاری سریع‌تر.
   - درخواست‌های ناوبری (صفحات Blazor) همیشه از شبکه می‌آیند؛ آفلاینِ واقعی
     برای Blazor Server ممکن نیست (SignalR لازم دارد).
   - رویدادهای push/notificationclick برای اعلان‌های وب‌پوش. */

const CACHE = 'packettransfer-shell-v1';
const CORE = [
    '/app.css',
    '/theme.js',
    '/lib/bootstrap/dist/css/bootstrap.rtl.min.css',
    '/lib/bootstrap-icons/font/bootstrap-icons.min.css',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/manifest.webmanifest'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE)
            .then((cache) => cache.addAll(CORE).catch(() => {}))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(
                keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const { request } = event;
    if (request.method !== 'GET') return;

    let url;
    try {
        url = new URL(request.url);
    } catch {
        return;
    }
    if (url.origin !== self.location.origin) return;

    // ناوبری صفحات: همیشه شبکه (کشِ صفحهٔ قدیمی بدتر از خطاست)
    if (request.mode === 'navigate') return;

    event.respondWith(
        caches.match(request).then((cached) => {
            const network = fetch(request).then((response) => {
                if (response && response.ok) {
                    const copy = response.clone();
                    caches.open(CACHE).then((cache) => cache.put(request, copy));
                }
                return response;
            }).catch(() => cached);
            return cached || network;
        })
    );
});

// --- اعلان‌های وب‌پوش (payload از PushSubscribeButton می‌آید؛ JSON: {title, body, url}) ---
self.addEventListener('push', (event) => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : '' };
    }

    event.waitUntil(
        self.registration.showNotification(data.title || 'سامانه', {
            body: data.body || '',
            dir: 'rtl',
            lang: 'fa',
            data: { url: data.url || '/' }
        })
    );
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const url = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(clients.openWindow(url));
});
