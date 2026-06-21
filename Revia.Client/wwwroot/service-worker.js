// Development service worker — intentionally does nothing, so debugging isn't
// hindered by cached assets. The production build swaps in
// service-worker.published.js (see the project's service worker publish step).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => { });
