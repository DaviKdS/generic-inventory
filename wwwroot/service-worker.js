const CACHE_NAME = "generic-inventory-v46-bed-fields";
const APP_SHELL = [
  "/",
  "/index.html",
  "/form",
  "/form.html",
  "/form.css?v=20261013-standard-blue",
  "/form.js?v=20261015-bed-fields",
  "/styles.css?v=20261013-light-theme-type",
  "/app.js?v=20261015-bed-fields-3",
  "/plasma-visual.js?v=20261013-plasma-motion",
  "/manifest.webmanifest",
  "/icons/icon.svg?v=20261013-globe-bed"
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL))
  );
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
    )
  );
  self.clients.claim();
});

self.addEventListener("fetch", (event) => {
  const requestUrl = new URL(event.request.url);

  if (requestUrl.origin !== self.location.origin || requestUrl.pathname.startsWith("/api/")) {
    return;
  }

  event.respondWith(
    fetch(event.request)
      .then((response) => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
        return response;
      })
      .catch(() => caches.match(event.request).then((cached) => cached || caches.match("/index.html")))
  );
});
