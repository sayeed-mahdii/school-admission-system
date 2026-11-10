declare global {
  interface Window {
    __APP_CONFIG__?: {
      API_URL?: string;
    };
  }
}

function resolveApiUrl(): string {
  const raw =
    window.__APP_CONFIG__?.API_URL ||
    import.meta.env.VITE_API_URL ||
    'http://localhost:8080';
  return raw.replace(/\/+$/, '');
}

export const API_URL = resolveApiUrl();

