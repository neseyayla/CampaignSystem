/**
 * Where the API lives.
 *
 * The same API the staff application talks to. Both are served from their own port and
 * both call this one address; nothing about the customer's screens runs on a separate
 * backend.
 */
export const API_BASE_URL = 'http://localhost:5284/api';
