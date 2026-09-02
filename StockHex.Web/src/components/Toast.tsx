import { useCallback, useMemo, useState, type ReactNode } from 'react';
import { Icon } from './Icon';
import { ApiError, NetworkError } from '../api/problem';
import { ToastContext, type ToastApi } from './toastContext';

type ToastTone = 'success' | 'error' | 'warn' | 'info';

interface Toast {
  id: number;
  tone: ToastTone;
  title: string;
  detail?: string;
}

const TONES: Record<ToastTone, { color: string; icon: string }> = {
  success: { color: 'var(--in)', icon: 'check' },
  error: { color: 'var(--dang)', icon: 'alert' },
  warn: { color: 'var(--out)', icon: 'alert' },
  info: { color: 'var(--acc)', icon: 'info' },
};

let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const push = useCallback((tone: ToastTone, title: string, detail?: string) => {
    const id = nextId++;
    setToasts((current) => [...current, { id, tone, title, detail }]);
    // Los errores se quedan más tiempo: hay que poder leer el detalle.
    window.setTimeout(() => dismiss(id), tone === 'error' ? 8000 : 4500);
  }, [dismiss]);

  const api = useMemo<ToastApi>(() => ({
    success: (title, detail) => push('success', title, detail),
    error: (title, detail) => push('error', title, detail),
    warn: (title, detail) => push('warn', title, detail),
    fromError: (error, fallbackTitle = 'No se pudo completar la operación') => {
      if (error instanceof NetworkError) {
        push('error', 'Sin conexión', error.message);
        return;
      }
      if (!(error instanceof ApiError)) {
        push('error', fallbackTitle, error instanceof Error ? error.message : undefined);
        return;
      }
      // Un 409 no es culpa del usuario: es una regla de negocio que lo bloquea,
      // así que se muestra como aviso y no como error rojo.
      if (error.isConflict) {
        push('warn', 'No se puede hacer', error.message);
        return;
      }
      if (error.isForbidden) {
        push('warn', 'Sin permiso', 'Tu rol no permite esta acción.');
        return;
      }
      if (error.isRateLimited) {
        push('warn', 'Demasiados intentos', error.message);
        return;
      }
      if (error.isValidation) {
        push('error', 'Datos inválidos', error.allFieldErrors[0] ?? error.message);
        return;
      }
      push('error', fallbackTitle, error.traceId ? `${error.message} (${error.traceId})` : error.message);
    },
  }), [push]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        aria-live="polite"
        style={{
          position: 'fixed', bottom: 20, right: 20, zIndex: 100,
          display: 'flex', flexDirection: 'column', gap: 10,
          width: 348, maxWidth: 'calc(100vw - 40px)', pointerEvents: 'none',
        }}
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            style={{
              display: 'flex', alignItems: 'flex-start', gap: 11,
              padding: '13px 14px', pointerEvents: 'auto',
              background: 'var(--surf)', border: '1px solid var(--bord)',
              borderLeft: `3px solid ${TONES[toast.tone].color}`,
              borderRadius: 7, boxShadow: 'var(--shadow-lg)',
              animation: 'shx-rise .16s ease-out',
            }}
          >
            <span style={{ color: TONES[toast.tone].color, marginTop: 1 }}>
              <Icon name={TONES[toast.tone].icon} size={17} />
            </span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 12.5, fontWeight: 600, color: TONES[toast.tone].color }}>
                {toast.title}
              </div>
              {toast.detail ? (
                <div style={{ fontSize: 11.5, color: 'var(--ink2)', marginTop: 3, lineHeight: 1.5 }}>
                  {toast.detail}
                </div>
              ) : null}
            </div>
            <button
              type="button"
              onClick={() => dismiss(toast.id)}
              aria-label="Cerrar aviso"
              style={{
                display: 'flex', padding: 2, background: 'transparent',
                border: 0, color: 'var(--ink2)', cursor: 'pointer', opacity: 0.5,
              }}
            >
              <Icon name="x" size={15} />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

