/**
 * Where the API lives.
 *
 * The HTTP port rather than HTTPS: the development certificate is self-signed, and the
 * browser blocks the call until it is trusted. The application listens on both.
 *
 * This moves into environment files once there is somewhere to deploy to.
 */
export const API_BASE_URL = '/api';
