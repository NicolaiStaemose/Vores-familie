// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
// Push notifications do work in development, so they can be tried on localhost.
self.importScripts('./push-handlers.js');

self.addEventListener('fetch', () => { });
