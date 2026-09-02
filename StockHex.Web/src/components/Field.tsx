import type { CSSProperties, ReactNode } from 'react';
import { Icon } from './Icon';

/* Bordes en propiedades largas, no en el atajo `border`: ERROR_RING sólo
   cambia el color, y mezclar atajo con propiedad larga entre renders hace que
   React avise de que una puede pisar a la otra. */
const CONTROL: CSSProperties = {
  width: '100%', height: 34, padding: '0 10px',
  background: 'var(--surf)', color: 'var(--ink)',
  borderWidth: 1, borderStyle: 'solid', borderColor: 'var(--bord2)',
  borderRadius: 'var(--r)',
  fontSize: 12.5, outline: 'none',
};

const ERROR_RING: CSSProperties = {
  borderColor: 'var(--dang)',
  boxShadow: '0 0 0 2px var(--dang-ring)',
};

interface WrapProps {
  label?: string;
  error?: string;
  hint?: string;
  width?: number | string;
  children: ReactNode;
  required?: boolean;
}

/** Etiqueta + control + mensaje de error pegado al campo que lo causó. */
export function Field({ label, error, hint, width, children, required }: WrapProps) {
  return (
    <label
      style={{
        display: 'flex', flexDirection: 'column', gap: 5,
        width: width ?? undefined,
        flex: width ? undefined : '1 1 0',
        minWidth: 0,
      }}
    >
      {label ? (
        <span style={{ fontSize: 11, fontWeight: 500, color: 'var(--ink2)' }}>
          {label}
          {required ? <span style={{ color: 'var(--dang)' }}> *</span> : null}
        </span>
      ) : null}
      {children}
      {error ? (
        <span
          style={{
            display: 'flex', alignItems: 'center', gap: 5,
            fontSize: 11.5, color: 'var(--dang)',
          }}
        >
          <Icon name="alert" size={12} />
          {error}
        </span>
      ) : hint ? (
        <span style={{ fontSize: 11, color: 'var(--ink3)' }}>{hint}</span>
      ) : null}
    </label>
  );
}

export function Input({
  value, onChange, placeholder, type = 'text', error, disabled, min, step, autoFocus, name,
}: {
  value: string | number;
  onChange: (value: string) => void;
  placeholder?: string;
  type?: 'text' | 'password' | 'email' | 'number' | 'date';
  error?: boolean;
  disabled?: boolean;
  min?: number;
  step?: number;
  autoFocus?: boolean;
  name?: string;
}) {
  return (
    <input
      name={name}
      type={type}
      value={value}
      min={min}
      step={step}
      disabled={disabled}
      autoFocus={autoFocus}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
      style={{
        ...CONTROL,
        ...(error ? ERROR_RING : null),
        ...(disabled ? { background: 'var(--surf3)', color: 'var(--ink3)' } : null),
        ...(type === 'number' ? { fontFamily: 'var(--mono)' } : null),
      }}
    />
  );
}

export function TextArea({
  value, onChange, placeholder, error, rows = 3,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  error?: boolean;
  rows?: number;
}) {
  return (
    <textarea
      value={value}
      rows={rows}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
      style={{
        ...CONTROL, height: 'auto', padding: '8px 10px',
        resize: 'vertical', lineHeight: 1.5,
        ...(error ? ERROR_RING : null),
      }}
    />
  );
}

export interface Option { value: string; label: string; }

export function Select({
  value, onChange, options, placeholder, error, disabled,
}: {
  value: string;
  onChange: (value: string) => void;
  options: Option[];
  placeholder?: string;
  error?: boolean;
  disabled?: boolean;
}) {
  return (
    <div style={{ position: 'relative', display: 'flex' }}>
      <select
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        style={{
          ...CONTROL,
          paddingRight: 28, appearance: 'none', cursor: disabled ? 'not-allowed' : 'pointer',
          ...(error ? ERROR_RING : null),
          ...(disabled ? { background: 'var(--surf3)', color: 'var(--ink3)' } : null),
        }}
      >
        {placeholder ? <option value="">{placeholder}</option> : null}
        {options.map((option) => (
          <option key={option.value} value={option.value}>{option.label}</option>
        ))}
      </select>
      <span
        aria-hidden
        style={{
          position: 'absolute', right: 9, top: '50%', transform: 'translateY(-50%)',
          color: 'var(--ink3)', pointerEvents: 'none',
        }}
      >
        <Icon name="down" size={14} />
      </span>
    </div>
  );
}

/** Búsqueda con icono. El debounce lo pone quien la usa. */
export function SearchInput({
  value, onChange, placeholder = 'Buscar…', width = 240,
}: { value: string; onChange: (value: string) => void; placeholder?: string; width?: number }) {
  return (
    <div style={{ position: 'relative', width }}>
      <span
        aria-hidden
        style={{
          position: 'absolute', left: 9, top: '50%', transform: 'translateY(-50%)',
          color: 'var(--ink3)', pointerEvents: 'none',
        }}
      >
        <Icon name="search" size={14} />
      </span>
      <input
        type="search"
        value={value}
        placeholder={placeholder}
        aria-label={placeholder}
        onChange={(event) => onChange(event.target.value)}
        style={{ ...CONTROL, paddingLeft: 28 }}
      />
      {value ? (
        <button
          type="button"
          onClick={() => onChange('')}
          aria-label="Limpiar búsqueda"
          style={{
            position: 'absolute', right: 6, top: '50%', transform: 'translateY(-50%)',
            display: 'flex', padding: 3, background: 'transparent',
            border: 0, borderRadius: 'var(--r-sm)', color: 'var(--ink3)', cursor: 'pointer',
          }}
        >
          <Icon name="x" size={13} />
        </button>
      ) : null}
    </div>
  );
}

/** Casilla que actúa como interruptor de filtro. */
export function Toggle({
  checked, onChange, label, tone = 'acc',
}: { checked: boolean; onChange: (checked: boolean) => void; label: string; tone?: 'acc' | 'danger' }) {
  const on = tone === 'danger'
    ? { color: 'var(--dang)', background: 'var(--dang-bg)', borderColor: 'var(--dang-bord)' }
    : { color: 'var(--acc)', background: 'var(--acc-soft)', borderColor: 'var(--acc-ring)' };

  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 7,
        height: 34, padding: '0 10px', fontSize: 12.5, fontWeight: 500,
        borderWidth: 1, borderStyle: 'solid', borderRadius: 'var(--r)',
        cursor: 'pointer', whiteSpace: 'nowrap',
        ...(checked
          ? on
          : { color: 'var(--ink2)', background: 'var(--surf)', borderColor: 'var(--bord2)' }),
      }}
    >
      <span
        aria-hidden
        style={{
          width: 14, height: 14, borderRadius: 3,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: checked ? 'currentColor' : 'transparent',
          borderWidth: checked ? 0 : 1,
          borderStyle: 'solid',
          borderColor: checked ? 'transparent' : 'var(--bord2)',
        }}
      >
        {checked ? (
          <span style={{ color: 'var(--surf)' }}><Icon name="check" size={11} strokeWidth={2.4} /></span>
        ) : null}
      </span>
      {label}
    </button>
  );
}

/** Fila de filtros sobre una tabla. */
export function FilterBar({ children, right }: { children: ReactNode; right?: ReactNode }) {
  return (
    <div
      style={{
        display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap',
        padding: '14px 16px', background: 'var(--surf2)',
        borderBottom: '1px solid var(--bord)',
      }}
    >
      {children}
      {right ? (
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'flex-end', gap: 8 }}>
          {right}
        </div>
      ) : null}
    </div>
  );
}
