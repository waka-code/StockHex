import type { AuthResponse, CurrentUserResponse } from '../api/types';

/**
 * Los tokens viven en localStorage para que la sesión sobreviva a recargar la
 * pestaña. Queda expuesto a XSS: la mitigación real es no tener XSS (React
 * escapa por defecto y no se usa dangerouslySetInnerHTML en ninguna parte).
 * La alternativa robusta sería una cookie HttpOnly, y eso lo tiene que emitir
 * la API; queda anotado como pendiente en el README.
 */
const KEY = 'stockhex.session';

export interface StoredSession {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUserResponse;
}

export function loadSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;

    const session = JSON.parse(raw) as StoredSession;
    if (!session?.accessToken || !session?.refreshToken || !session?.user) return null;

    // Si el refresco ya venció no hay nada que renovar: se descarta y se pide login.
    if (new Date(session.refreshTokenExpiresAt).getTime() <= Date.now()) {
      clearSession();
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function saveSession(auth: AuthResponse): StoredSession {
  const session: StoredSession = {
    accessToken: auth.accessToken,
    expiresAt: auth.expiresAt,
    refreshToken: auth.refreshToken,
    refreshTokenExpiresAt: auth.refreshTokenExpiresAt,
    user: auth.user,
  };
  try {
    localStorage.setItem(KEY, JSON.stringify(session));
  } catch {
    // Modo privado o almacenamiento lleno: la sesión sigue en memoria y sólo se
    // pierde al recargar. No es motivo para impedir el login.
  }
  return session;
}

export function clearSession(): void {
  try {
    localStorage.removeItem(KEY);
  } catch { /* nada que hacer */ }
}

/** Fecha en que conviene renovar: un minuto antes de expirar, para no cortar en medio. */
export function shouldRefresh(session: StoredSession): boolean {
  return new Date(session.expiresAt).getTime() - 60_000 <= Date.now();
}
