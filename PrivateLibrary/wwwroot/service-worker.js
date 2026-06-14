// App-Shell Service Worker for Private Library
// Caches static assets for faster loads and basic offline shell.
//
// NOTE: This provides installability and asset caching only.
// Full offline data access (Lending, Movies) requires a future
// migration to Blazor WASM PWA + API architecture.

const CACHE_NAME = 'privatelibrary-shell-v4';

const SHELL_ASSETS = [
  '/',
  '/account/login',
  '/app.css',
  '/PrivateLibrary.styles.css',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/images/placeholder.png',
  '/images/icon-192.png',
  '/images/icon-512.png',
  '/images/apple-touch-icon.png',
  '/favicon.png',
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache =>
      cache.addAll(SHELL_ASSETS.map(url => new Request(url, { cache: 'reload' })))
        .catch(() => {})
    )
  );
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // Only handle same-origin GET requests
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;

  // Images: cache-first
  if (url.pathname.startsWith('/images/')) {
    event.respondWith(
      caches.match(event.request).then(cached => cached || fetch(event.request).then(resp => {
        const clone = resp.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
        return resp;
      }))
    );
    return;
  }

  // Static assets (css, js, fonts): cache-first
  if (url.pathname.startsWith('/lib/') || url.pathname.startsWith('/_framework/') ||
      url.pathname.endsWith('.css') || url.pathname.endsWith('.js')) {
    event.respondWith(
      caches.match(event.request).then(cached => cached || fetch(event.request))
    );
    return;
  }

  // Everything else: network-first (app requires online for data)
  event.respondWith(
    fetch(event.request).catch(() => caches.match('/account/login') || caches.match('/'))
  );
});
