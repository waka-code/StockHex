import { useEffect, useRef, type ReactNode } from 'react';
import { Icon } from './Icon';

interface Props {
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  width?: number;
}

export function Modal({ title, subtitle, onClose, children, footer, width = 480 }: Props) {
  const panel = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKey);

    // Se bloquea el scroll del fondo para que la rueda no mueva la tabla de atrás.
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    panel.current?.focus();

    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = previous;
    };
  }, [onClose]);

  return (
    <div
      role="presentation"
      onMouseDown={(event) => {
        // Sólo cierra si el clic empezó en el fondo, no si se arrastró desde dentro.
        if (event.target === event.currentTarget) onClose();
      }}
      style={{
        position: 'fixed', inset: 0, zIndex: 50,
        background: 'var(--scrim)',
        display: 'flex', alignItems: 'flex-start', justifyContent: 'center',
        padding: '64px 20px 20px', overflowY: 'auto',
        animation: 'shx-fade .14s ease-out',
      }}
    >
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        style={{
          width: '100%', maxWidth: width, outline: 'none',
          background: 'var(--surf)', border: '1px solid var(--bord)',
          borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-lg)',
          overflow: 'hidden', animation: 'shx-rise .16s ease-out',
        }}
      >
        <div
          style={{
            display: 'flex', alignItems: 'center', gap: 12,
            padding: '15px 18px', borderBottom: '1px solid var(--bord)',
          }}
        >
          <div style={{ minWidth: 0 }}>
            <div style={{ fontSize: 14.5, fontWeight: 600, letterSpacing: '-.02em' }}>{title}</div>
            {subtitle ? (
              <div style={{ fontSize: 11.5, color: 'var(--ink3)', marginTop: 2 }}>{subtitle}</div>
            ) : null}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar"
            style={{
              marginLeft: 'auto', display: 'flex', padding: 4,
              background: 'transparent', border: 0, borderRadius: 'var(--r-sm)',
              color: 'var(--ink2)', cursor: 'pointer', opacity: 0.6,
            }}
          >
            <Icon name="x" size={17} />
          </button>
        </div>

        <div style={{ padding: 18, display: 'flex', flexDirection: 'column', gap: 14 }}>
          {children}
        </div>

        {footer ? (
          <div
            style={{
              display: 'flex', alignItems: 'center', gap: 9,
              padding: '13px 18px', background: 'var(--surf2)',
              borderTop: '1px solid var(--bord)',
            }}
          >
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  );
}

/** Confirmación para acciones destructivas o irreversibles. */
export function ConfirmModal({
  title, message, confirmLabel, onConfirm, onClose, loading, tone = 'danger',
}: {
  title: string;
  message: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onClose: () => void;
  loading?: boolean;
  tone?: 'danger' | 'primary';
}) {
  return (
    <Modal title={title} onClose={onClose} width={420}>
      <div style={{ fontSize: 12.5, color: 'var(--ink2)', lineHeight: 1.6 }}>{message}</div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
        <button
          type="button"
          onClick={onClose}
          style={{
            padding: '7px 12px', fontSize: 12.5, fontWeight: 500,
            background: 'var(--surf)', color: 'var(--ink)',
            border: '1px solid var(--bord2)', borderRadius: 'var(--r)', cursor: 'pointer',
          }}
        >
          Cancelar
        </button>
        <button
          type="button"
          onClick={onConfirm}
          disabled={loading}
          style={{
            padding: '7px 12px', fontSize: 12.5, fontWeight: 500,
            background: tone === 'danger' ? 'var(--dang)' : 'var(--acc)',
            color: '#fff', border: 0, borderRadius: 'var(--r)',
            cursor: loading ? 'not-allowed' : 'pointer', opacity: loading ? 0.5 : 1,
          }}
        >
          {confirmLabel}
        </button>
      </div>
    </Modal>
  );
}
