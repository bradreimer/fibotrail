/*
 * The service worker. It caches the app shell: the page, its scripts, styles, fonts, and icons.
 * It never caches an API answer or a photo, so your trips always come from the server.
 * The build replaces the two placeholders below. See vite-plugins/service-worker.ts.
 */
const VERSION = '__FIBOTRAIL_VERSION__';
const PRECACHE = self.__FIBOTRAIL_PRECACHE__ ?? [];

const CACHE = `fibotrail-shell-${VERSION}`;
const SHELL = '/';

/** Paths the server answers itself. The shell is never served in their place. */
const SERVER_PATHS = ['/api/', '/sign-in', '/sign-out', '/setup', '/healthz', '/openapi/'];

/** Static files that are safe to keep: they hold no personal data. */
const STATIC_PATHS = ['/assets/', '/icons/', '/splash/', '/manifest.webmanifest'];

/** Caches one address, unless the server answered with an error or sent the browser elsewhere. */
async function store(cache, url) {
  const response = await fetch(url, { credentials: 'same-origin' });
  // A redirect means the session ended. The sign-in screen must never be kept as the shell.
  if (response.ok && !response.redirected) await cache.put(url, response);
}

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => Promise.all([SHELL, ...PRECACHE].map((url) => store(cache, url))))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names
            .filter((name) => name.startsWith('fibotrail-shell-') && name !== CACHE)
            .map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin) return;

  const isServerPath = SERVER_PATHS.some(
    (path) => url.pathname === path || url.pathname.startsWith(path),
  );
  if (request.mode === 'navigate' && !isServerPath) {
    // The cached shell answers at once. A fresh copy is fetched for the next visit.
    event.respondWith(
      caches.open(CACHE).then(async (cache) => {
        const cached = await cache.match(SHELL);
        const refresh = store(cache, SHELL).catch(() => undefined);
        if (cached) return cached;
        await refresh;
        return (await cache.match(SHELL)) ?? fetch(request);
      }),
    );
    return;
  }

  if (STATIC_PATHS.some((path) => url.pathname.startsWith(path))) {
    event.respondWith(
      caches.open(CACHE).then(async (cache) => {
        const cached = await cache.match(request);
        if (cached) return cached;
        const response = await fetch(request);
        if (response.ok) await cache.put(request, response.clone());
        return response;
      }),
    );
  }
});
