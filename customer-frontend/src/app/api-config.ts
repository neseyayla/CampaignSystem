/**
 * Where the API lives — a same-origin path, not an absolute host.
 *
 * The page is served by nginx (or the dev server), which proxies "/api" to the actual API.
 * Keeping it relative means the app works wherever it is reached from — localhost, a LAN
 * address, or a public tunnel — without baking a host name that would only be right on one
 * machine. In Docker the proxy lives in nginx.conf; for "ng serve" it lives in proxy.conf.json.
 */
export const API_BASE_URL = '/api';
