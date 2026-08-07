// wwwroot/js/connectivity.js
// Reports browser online/offline transitions to .NET, and exposes a haptic tap.
// Used by the game point tracker, which must keep working with no reception.

let dotnetRef = null;
let onlineHandler = null;
let offlineHandler = null;

export function start(dotnetInstance) {
    stop();

    dotnetRef = dotnetInstance;

    onlineHandler = () => {
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnConnectivityChanged', true).catch(() => { });
    };
    offlineHandler = () => {
        if (dotnetRef) dotnetRef.invokeMethodAsync('OnConnectivityChanged', false).catch(() => { });
    };

    window.addEventListener('online', onlineHandler);
    window.addEventListener('offline', offlineHandler);

    return navigator.onLine !== false;
}

export function stop() {
    if (onlineHandler) window.removeEventListener('online', onlineHandler);
    if (offlineHandler) window.removeEventListener('offline', offlineHandler);

    onlineHandler = null;
    offlineHandler = null;
    dotnetRef = null;
}

export function isOnline() {
    return navigator.onLine !== false;
}

export function vibrate(ms) {
    try {
        if (navigator.vibrate) navigator.vibrate(ms);
    } catch { /* not supported, no-op */ }
}
