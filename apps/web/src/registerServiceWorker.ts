/**
 * Registers the service worker that caches the app shell. It runs only in a production build,
 * and only where the browser allows it: over HTTPS, or on localhost.
 */
export function registerServiceWorker(): void {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return;
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // The app works without the cache. It only starts slower.
    });
  });
}
