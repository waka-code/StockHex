import type { CSSProperties, ReactNode } from 'react';
import { Icon } from './Icon';
import type { MovementType } from '../api/types';
import {
  BUTTON_STYLES, CHIP_TONES, MOVEMENT, NOTE_TONES,
  type ButtonKind, type ChipTone, type NoteTone,
} from './tokens';

// ───────────────────────────────────────────────────────── botón

interface ButtonProps {
  children?: ReactNode;
  kind?: ButtonKind;
  icon?: string;
  size?: 'md' | 'sm';
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  type?: 'button' | 'submit';
  /** Id del formulario a enviar cuando el botón vive fuera de él. */
  form?: string;
  title?: string;
  style?: CSSProperties;
  full?: boolean;
}

export function Button({
  children, kind = 'ghost', icon, size = 'md', onClick,
  disabled, loading, type = 'button', form, title, style, full,
}: ButtonProps) {
  const isDisabled = disabled || loading;
  return (
    <button
      type={type}
      form={form}
      onClick={onClick}
      disabled={isDisabled}
      title={title}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        gap: 6,
        padding: size === 'md' ? '7px 12px' : '5px 9px',
        fontSize: size === 'md' ? 12.5 : 12,
        fontWeight: 500, letterSpacing: '-.01em', whiteSpace: 'nowrap',
        borderWidth: 1, borderStyle: 'solid', borderRadius: 'var(--r)',
        cursor: isDisabled ? 'not-allowed' : 'pointer',
        opacity: isDisabled ? 0.45 : 1,
        width: full ? '100%' : undefined,
        transition: 'opacity .12s, filter .12s',
        ...BUTTON_STYLES[kind],
        ...style,
      }}
    >
      {loading ? <Icon name="spinner" size={14} spin /> : icon ? <Icon name={icon} size={14} /> : null}
      {children}
    </button>
  );
}

/** Botón sólo-icono para las acciones de fila. */
export function IconButton({
  icon, onClick, title, disabled, tone = 'ink2',
}: { icon: string; onClick?: () => void; title: string; disabled?: boolean; tone?: 'ink2' | 'dang' }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={title}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        width: 26, height: 26, padding: 0,
        background: 'transparent', border: '1px solid transparent',
        borderRadius: 'var(--r-sm)',
        color: tone === 'dang' ? 'var(--dang)' : 'var(--ink2)',
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.28 : 0.72,
      }}
    >
      <Icon name={icon} size={15} />
    </button>
  );
}

// ───────────────────────────────────────────────────────── chip

export function Chip({
  children, tone = 'neutral', icon,
}: { children: ReactNode; tone?: ChipTone; icon?: string }) {
  return (
    <span
      style={{
        display: 'inline-flex', alignItems: 'center', gap: icon ? 4 : 0,
        padding: '2px 7px', fontSize: 11, fontWeight: 500,
        lineHeight: 1.5, whiteSpace: 'nowrap',
        borderWidth: 1, borderStyle: 'solid', borderRadius: 'var(--r-sm)',
        ...CHIP_TONES[tone],
      }}
    >
      {icon ? <Icon name={icon} size={12} /> : null}
      {children}
    </span>
  );
}

export function MovementChip({ type }: { type: MovementType }) {
  const meta = MOVEMENT[type];
  return <Chip tone={meta.tone} icon={meta.icon}>{meta.label}</Chip>;
}

/** Cantidad con el signo que corresponde al tipo: +240, −36, =41. */
export function MovementQuantity({ type, quantity }: { type: MovementType; quantity: number }) {
  const sign = type === 'In' ? '+' : type === 'Out' ? '−' : '=';
  return (
    <span className="num" style={{ color: MOVEMENT[type].color, fontWeight: 500 }}>
      {sign}{quantity}
    </span>
  );
}

// ───────────────────────────────────────────────────────── tarjeta

export function Card({
  children, pad = 16, style,
}: { children: ReactNode; pad?: number | false; style?: CSSProperties }) {
  return (
    <section
      style={{
        background: 'var(--surf)', border: '1px solid var(--bord)',
        borderRadius: 'var(--r-lg)', boxShadow: 'var(--shadow)',
        overflow: 'hidden', minWidth: 0,
        ...style,
      }}
    >
      {pad === false ? children : <div style={{ padding: pad }}>{children}</div>}
    </section>
  );
}

export function CardHead({
  title, sub, right,
}: { title: ReactNode; sub?: ReactNode; right?: ReactNode }) {
  return (
    <div
      style={{
        display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap',
        padding: '13px 16px', borderBottom: '1px solid var(--bord)',
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 13, fontWeight: 600, letterSpacing: '-.01em' }}>{title}</div>
        {sub ? <div style={{ fontSize: 11, color: 'var(--ink3)', marginTop: 2 }}>{sub}</div> : null}
      </div>
      {right ? (
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
          {right}
        </div>
      ) : null}
    </div>
  );
}

// ───────────────────────────────────────────────────────── KPI

export function Kpi({
  label, value, foot, tone, icon,
}: { label: string; value: ReactNode; foot?: ReactNode; tone?: string; icon?: string }) {
  return (
    <section
      style={{
        // `flex: 1` a secas es `1 1 0%`: con base cero las tarjetas nunca
        // envuelven y se reparten el ancho que haya, por poco que sea. A 390px
        // quedaban en 77px, y una etiqueta de una sola palabra —«REVERSIONES»,
        // 81px— no cabe ni puede partirse, así que empujaba el icono fuera del
        // documento y la página entera desplazaba en horizontal. Con una base
        // real la fila pasa de cuatro columnas a dos y a una.
        flex: '1 1 170px', minWidth: 0, background: 'var(--surf)',
        border: '1px solid var(--bord)', borderRadius: 'var(--r-lg)',
        padding: '14px 16px', boxShadow: 'var(--shadow)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
        <div
          style={{
            fontSize: 11, fontWeight: 500, color: 'var(--ink2)',
            textTransform: 'uppercase', letterSpacing: '.06em',
            // Si aun así no cupiera, que se parta la palabra antes que
            // desbordar: el desborde lo paga el documento entero.
            minWidth: 0, overflowWrap: 'anywhere',
          }}
        >
          {label}
        </div>
        {icon ? (
          <div style={{ marginLeft: 'auto', opacity: 0.4, color: 'var(--ink3)' }}>
            <Icon name={icon} size={18} />
          </div>
        ) : null}
      </div>
      <div
        className="num"
        style={{
          fontSize: 26, fontWeight: 500, marginTop: 6,
          letterSpacing: '-.03em', lineHeight: 1.1,
          color: tone ?? 'var(--ink)',
        }}
      >
        {value}
      </div>
      {foot ? (
        <div
          style={{
            display: 'flex', alignItems: 'center', gap: 5, marginTop: 8,
            fontSize: 11, color: 'var(--ink3)',
          }}
        >
          {foot}
        </div>
      ) : null}
    </section>
  );
}

// ───────────────────────────────────────────────────────── aviso

export function Note({
  children, tone = 'neutral', icon = 'info',
}: { children: ReactNode; tone?: NoteTone; icon?: string }) {
  return (
    <div
      style={{
        display: 'flex', alignItems: 'flex-start', gap: 9,
        padding: '10px 12px', fontSize: 12, lineHeight: 1.55,
        borderWidth: 1, borderStyle: 'solid', borderRadius: 'var(--r)',
        ...NOTE_TONES[tone],
      }}
    >
      <span style={{ marginTop: 1 }}><Icon name={icon} size={15} /></span>
      <span style={{ minWidth: 0 }}>{children}</span>
    </div>
  );
}

// ───────────────────────────────────────────────── estados vacíos

export function EmptyState({
  title, detail, icon = 'empty', action,
}: { title: string; detail?: string; icon?: string; action?: ReactNode }) {
  return (
    <div
      style={{
        padding: '52px 24px', display: 'flex', flexDirection: 'column',
        alignItems: 'center', gap: 13, animation: 'shx-fade .18s ease-out',
      }}
    >
      <span
        style={{
          width: 44, height: 44, borderRadius: 10, background: 'var(--surf3)',
          border: '1px solid var(--bord)', color: 'var(--ink3)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}
      >
        <Icon name={icon} size={22} />
      </span>
      <div style={{ textAlign: 'center', maxWidth: 380 }}>
        <div style={{ fontSize: 13.5, fontWeight: 500 }}>{title}</div>
        {detail ? (
          <div style={{ fontSize: 12, color: 'var(--ink3)', marginTop: 4, lineHeight: 1.55 }}>
            {detail}
          </div>
        ) : null}
      </div>
      {action ? <div style={{ display: 'flex', gap: 8, marginTop: 2 }}>{action}</div> : null}
    </div>
  );
}

export function Spinner({ size = 18, label }: { size?: number; label?: string }) {
  return (
    <div
      style={{
        display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 9,
        padding: '44px 20px', color: 'var(--ink3)', fontSize: 12,
      }}
    >
      <Icon name="spinner" size={size} spin />
      {label ? <span>{label}</span> : null}
      <span className="sr-only">Cargando</span>
    </div>
  );
}

/** Barra de proporción para los reportes. */
export function Bar({ value, max, color }: { value: number; max: number; color: string }) {
  const pct = max > 0 ? Math.min(100, Math.round((value / max) * 100)) : 0;
  return (
    <span
      style={{
        display: 'block', flex: 1, height: 6, borderRadius: 3,
        background: 'var(--surf3)', overflow: 'hidden',
      }}
    >
      <span style={{ display: 'block', height: 6, width: `${pct}%`, background: color, borderRadius: 3 }} />
    </span>
  );
}
