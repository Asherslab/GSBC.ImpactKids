// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => onFetch(event));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

// Requests that must never be answered from cache, and must never be mistaken for the
// app shell. A miss here has to surface as a miss, not as index.html.
function isFrameworkAsset(url) {
    return url.pathname.startsWith(`${base}_framework/`);
}

function isPassThrough(url) {
    return url.pathname.startsWith(`${base}bff/`)
        || url.pathname.startsWith(`${base}gRPC/`)
        || url.pathname.startsWith(`${base}public/`)
        || url.pathname.startsWith(`${base}api/`);
}

async function onInstall(event) {
    console.info('Service worker: Install');

    // Take over as soon as this worker is installed. Paired with clients.claim() below,
    // this is what stops an old worker from serving a stale shell after a deploy.
    self.skipWaiting();

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);

    // Deliberately NOT cache.addAll: that rejects the whole install if any single asset
    // fails, which leaves every client stuck on the previous worker indefinitely. Cache
    // what we can and let anything missing fall through to the network at runtime.
    const results = await Promise.allSettled(
        assetsRequests.map(async request => {
            const response = await fetch(request);

            if (!response.ok)
                throw new Error(`${request.url} -> ${response.status}`);

            await cache.put(request, response);
        })
    );

    const failed = results.filter(x => x.status === 'rejected');

    if (failed.length)
        console.warn(`Service worker: ${failed.length} asset(s) not precached`, failed.map(x => String(x.reason)));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Claim BEFORE dropping old caches. The old worker keeps controlling open pages until
    // it is replaced, so deleting its cache first pulls assets out from under a live page:
    // it then falls through to the network for hashes the new deploy no longer has, the
    // SPA fallback answers those with index.html, and the integrity check fails.
    await self.clients.claim();

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    await recoverStaleClients();
}

/*
    A page still running the previous shell is already dead by the time we get here: it
    booted against asset hashes this deploy replaced and failed before rendering. It cannot
    heal itself, because the recovery script only exists in the *new* index.html, which such
    a page never received - so without this it takes two manual reloads.

    Now that this worker has claimed them, re-navigating a client is served by us, network
    first, off the current shell. Runs once per activation, so it cannot loop.
*/
async function recoverStaleClients() {
    let windows = [];

    try {
        windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: false });
    } catch {
        return;
    }

    await Promise.all(windows.map(async client => {
        // navigate() is the only thing that reaches a page with no script of ours running.
        // Not available everywhere, so fall back to a message the new shell listens for.
        try {
            if (typeof client.navigate === 'function') {
                await client.navigate(client.url);
                return;
            }
        } catch {
            // Cross-origin or disallowed - fall through to the message.
        }

        try {
            client.postMessage({ type: 'sw-updated' });
        } catch {
            // Nothing more we can do from here.
        }
    }));
}

function onFetch(event) {
    const request = event.request;

    if (request.method !== 'GET')
        return;

    const url = new URL(request.url);

    // Leave anything we do not own alone - cross origin, and the API surface.
    if (url.origin !== self.origin || isPassThrough(url))
        return;

    event.respondWith(handleFetch(request, url));
}

async function handleFetch(request, url) {
    const cache = await caches.open(cacheName);

    // Fingerprinted, so a hit is always valid and a miss must go to the network.
    if (isFrameworkAsset(url)) {
        const cached = await cache.match(request);

        if (cached)
            return cached;

        const response = await fetch(request);

        // The SPA fallback answers a missing asset with index.html and a 200. Handing that
        // back to the runtime produces a bewildering integrity error, so refuse it here.
        if (isHtml(response))
            return new Response('', { status: 404, statusText: 'Not Found' });

        return response;
    }

    const shouldServeIndexHtml = request.mode === 'navigate'
        && !manifestUrlList.some(x => x === request.url);

    if (shouldServeIndexHtml) {
        // Network first for the shell. Cache first was how a stale app shell survived a
        // deploy forever: the page kept booting against asset hashes that were long gone.
        try {
            const response = await fetch(request);

            if (response.ok) {
                await cache.put('index.html', response.clone());
                return response;
            }
        } catch {
            // Offline - fall through to whatever shell we have.
        }

        return await cache.match('index.html') ?? fetch(request);
    }

    let cached = await cache.match(request);

    if (cached && cached.redirected)
        cached = new Response(cached.body, {
            headers: cached.headers,
            status: cached.status,
            statusText: cached.statusText
        });

    return cached ?? fetch(request);
}

function isHtml(response) {
    return (response.headers.get('content-type') ?? '').includes('text/html');
}
