import { ApiError, NetworkError, readProblem } from './problem';
import type { AuthResponse } from './types';
import {
  clearSession, loadSession, saveSession, shouldRefresh,
  type StoredSession,
} from '../auth/storage';

declare global {
  interface Window {
    /** Lo escribe el contenedor al arrancar, desde la variable API_URL. */
    __STOCKHEX_CONFIG__?: { apiUrl?: string };
  }
}

/**
 * Vite congela `import.meta.env` al compilar, así que una imagen Docker quedaría
 * atada a la URL con la que se construyó. Por eso se mira primero la
 * configuración que el contenedor escribe al arrancar.
 *
 * Vacío significa "mismo origen": es el caso del despliegue con Docker, donde
 * nginx sirve la aplicación y hace de proxy de /api, así que no hay CORS.
 */
function resolveBaseUrl(): string {
  const runtime = window.__STOCKHEX_CONFIG__?.apiUrl;
  const configured = runtime ?? import.meta.env.VITE_API_URL ?? '';

  return configured.trim().replace(/\/$/, '');
}

const BASE_URL = resolveBaseUrl();

/**
 * La sesión vive en este módulo, no en el estado de React: el cliente HTTP tiene
 * que poder renovar el token desde cualquier petición, incluso las que se
 * disparan fuera de un render.
 */
let session: StoredSession | null = loadSession();

type Listener = (session: StoredSession | null) => void;
const listeners = new Set<Listener>();

export function getSession(): StoredSession | null {
  return session;
}

export function setSession(auth: AuthResponse | null): void {
  session = auth ? saveSession(auth) : null;
  if (!auth) clearSession();
  listeners.forEach((listener) => listener(session));
}

export function onSessionChange(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

// ─────────────────────────────────────────────── renovación de token

/**
 * Una sola renovación en vuelo a la vez. Sin esto, cinco peticiones que reciben
 * 401 al mismo tiempo dispararían cinco canjes del mismo refresh token; el
 * primero lo rota y los otros cuatro llegan con un token ya usado, lo que la API
 * interpreta como reutilización y corta la sesión completa.
 */
let refreshInFlight: Promise<StoredSession | null> | null = null;

async function refreshSession(): Promise<StoredSession | null> {
  if (refreshInFlight) return refreshInFlight;

  const current = session;
  if (!current) return null;

  refreshInFlight = (async () => {
    try {
      const response = await fetch(buildUrl('/api/auth/refresh'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      });

      if (!response.ok) {
        // El refresco no sirve (venció, se revocó o se detectó reutilización):
        // no hay forma de recuperarse, hay que volver a autenticarse.
        setSession(null);
        return null;
      }

      const auth = (await response.json()) as AuthResponse;
      setSession(auth);
      return session;
    } catch {
      // Fallo de red: se conserva la sesión, quizá la siguiente petición funcione.
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

// ─────────────────────────────────────────────────── petición base

/** Los filtros son interfaces sin index signature, de ahí el `object`. */
function buildUrl(path: string, query?: object): string {
  // Con BASE_URL vacío las rutas son relativas al origen que sirve la página.
  const url = new URL(`${BASE_URL}${path}`, window.location.origin);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      // Se omiten los vacíos para no mandar "?categoryId=" y que la API
      // intente parsear una cadena vacía como Guid.
      if (value === undefined || value === null || value === '') continue;
      url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  query?: object;
  /** Los endpoints de login, registro y refresco no llevan token. */
  anonymous?: boolean;
  signal?: AbortSignal;
}

async function send<T>(path: string, options: RequestOptions, retrying = false): Promise<T> {
  const { method = 'GET', body, query, anonymous = false, signal } = options;

  // Renovación anticipada: si el access token está por vencer se canjea antes de
  // salir, para no gastar un 401 y una segunda ida al servidor.
  if (!anonymous && session && shouldRefresh(session) && !retrying) {
    await refreshSession();
  }

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (!anonymous && session) headers.Authorization = `Bearer ${session.accessToken}`;

  let response: Response;
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  } catch (cause) {
    if (signal?.aborted) throw cause;
    throw new NetworkError(cause);
  }

  if (response.status === 401 && !anonymous && !retrying && session) {
    const renewed = await refreshSession();
    if (renewed) return send<T>(path, options, true);
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response), response.statusText);
  }

  // 204 y 205 no traen cuerpo; devolverlo como undefined evita un JSON.parse('').
  if (response.status === 204 || response.status === 205) return undefined as T;

  const type = response.headers.get('content-type') ?? '';
  if (!type.includes('json')) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, query?: object, signal?: AbortSignal) =>
    send<T>(path, { query, signal }),

  post: <T>(path: string, body?: unknown) =>
    send<T>(path, { method: 'POST', body }),

  put: <T>(path: string, body?: unknown) =>
    send<T>(path, { method: 'PUT', body }),

  del: <T>(path: string) =>
    send<T>(path, { method: 'DELETE' }),

  /** Sin token: sólo login, registro y refresco. */
  anon: <T>(path: string, body?: unknown) =>
    send<T>(path, { method: 'POST', body, anonymous: true }),
};

export { BASE_URL };
