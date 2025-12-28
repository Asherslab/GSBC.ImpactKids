
// wwwroot/js/sseEventSource.js
// Native EventSource-based SSE client (no auth headers).
// Accepts URL from .NET, manages reconnect/backoff, persists Last-Event-ID,
// and notifies .NET via DotNetObjectReference.

let es = null;
let dotnetRef = null;
let currentUrl = null;

let reconnectTimer = null;
let reconnectDelayMs = 2000;         // initial backoff
const maxReconnectDelayMs = 30000;   // cap backoff

let isStopping = false;
let lastEventId = null;

let onlineHandler = null;
let offlineHandler = null;

function clearReconnect() {
    if (reconnectTimer) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
    }
}

function backoff() {
    reconnectDelayMs = Math.min(reconnectDelayMs * 2, maxReconnectDelayMs);
}

function resetBackoff() {
    reconnectDelayMs = 2000;
}

function storeLastEventId(id) {
    if (!id) return;
    lastEventId = id;
    try { localStorage.setItem('sse:lastEventId', id); } catch { /* ignore */ }
}

function readStoredLastEventId() {
    try { return localStorage.getItem('sse:lastEventId'); } catch { return null; }
}

function closeInternal() {
    clearReconnect();
    if (es) {
        try { es.close(); } catch { /* ignore */ }
        es = null;
    }
}

async function safeInvoke(method, ...args) {
    if (!dotnetRef) return;
    try { await dotnetRef.invokeMethodAsync(method, ...args); } catch { /* ignore */ }
}

async function connect() {
    closeInternal();
    isStopping = false;

    // Attempt resume
    const resumeId = readStoredLastEventId();
    if (resumeId && !lastEventId) lastEventId = resumeId;

    try {
        // Note: Standard EventSource only takes { withCredentials } as options.
        // If your server honors resuming via Last-Event-ID, the browser will
        // send it automatically after a redirect/reconnect. Some servers require
        // initial request header; EventSource will include Last-Event-ID during
        // certain navigation flows. We still track/persist it for diagnostics.
        es = new EventSource(currentUrl, {withCredentials: false});

        es.onopen = async () => {
            await safeInvoke('OnOpen');
            resetBackoff();
        };

        es.onmessage = async (evt) => {
            // evt.data is a string (possibly multi-line); evt.lastEventId may be set
            const data = evt.data ?? '';
            const id = evt.lastEventId ?? '';
            const type = 'message'; // native EventSource exposes only onmessage/onerror; typed events use addEventListener

            if (id) storeLastEventId(id);
            await safeInvoke('OnMessage', data, id, type);
        };

        es.onerror = async () => {
            // onerror fires on transient network issues or when server closes.
            await safeInvoke('OnError', 'SSE error/disconnected');
            closeInternal();
            scheduleReconnect();
        };

        // If you expect named events: server sends "event: foo"
        // You can add listeners via addEventListener:
        // es.addEventListener('foo', (evt) => { ... });
    } catch (ex) {
        await safeInvoke('OnError', `Failed to start EventSource: ${ex}`);
        scheduleReconnect();
    }
}

function scheduleReconnect() {
    clearReconnect();
    backoff();
    reconnectTimer = setTimeout(connect, reconnectDelayMs);
}

export async function start(url, dotnetInstance) {
    currentUrl = url;
    dotnetRef = dotnetInstance;

    onlineHandler = async () => {
        if (!isStopping) {
            clearReconnect();
            resetBackoff();
            await connect();
        }
    };
    offlineHandler = () => {
        // Abort by closing; browser will auto-retry later
        closeInternal();
    };

    window.addEventListener('online', onlineHandler);
    window.addEventListener('offline', offlineHandler);

    await connect();
}

// noinspection JSUnusedGlobalSymbols
export async function stop() {
    isStopping = true;
    closeInternal();

    window.removeEventListener('online', onlineHandler);
    window.removeEventListener('offline', offlineHandler);

    await safeInvoke('OnStopped');
    dotnetRef = null;
}

// noinspection JSUnusedGlobalSymbols
export function clearResumeToken() {
    try { localStorage.removeItem('sse:lastEventId'); } catch { }
    lastEventId = null;
}
