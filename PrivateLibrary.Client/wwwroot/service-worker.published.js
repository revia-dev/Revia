// Production service worker for the Revia PWA.
//
// Precaches the WASM app shell + framework + static assets (from the generated
// service-worker-assets.js manifest) so the app starts offline. Navigation
// requests fall back to the cached index.html when the network is unavailable
// (enabling "close the app, reopen offline").
//
// IMPORTANT: API calls (/api/**) and auth are NEVER cached here — data lives in
// the IndexedDB offline layer (read-cache + outbox), so the service worker must
// not interfere. Those requests always go straight to the network; offline data
// access is handled in the app, not here.

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'revia-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// Only precache the app shell and static assets — not source maps, not data.
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css/, /\.woff/, /\.png/, /\.jpe?g/, /\.gif/, /\.ico/, /\.blat/, /\.dat$/, /\.webmanifest$/, /\.webp$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

// Serve the SPA shell for navigations.
const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, self.origin).href);

async function onInstall() {
    self.skipWaiting();

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
    self.clients.claim();
    // Delete older caches.
    const keys = await caches.keys();
    await Promise.all(keys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    const request = event.request;
    const url = new URL(request.url);

    // Never touch API / auth calls — they belong to the offline data layer.
    if (url.pathname.startsWith('/api/')) {
        return fetch(request);
    }

    if (request.method !== 'GET') {
        return fetch(request);
    }

    // For navigations, serve the cached app shell (index.html) so the app boots
    // offline; for other GETs, serve the cached asset when present.
    const shouldServeIndexHtml = request.mode === 'navigate';
    const cache = await caches.open(cacheName);

    if (shouldServeIndexHtml) {
        const cachedIndex = await cache.match('index.html');
        if (cachedIndex) {
            // Try the network first (fresh shell) but fall back to cache offline.
            try {
                return await fetch(request);
            } catch {
                return cachedIndex;
            }
        }
    }

    const cached = await cache.match(request);
    if (cached) {
        return cached;
    }

    try {
        return await fetch(request);
    } catch {
        // Last resort for asset requests when fully offline.
        const fallback = await cache.match('index.html');
        return fallback ?? Response.error();
    }
}
