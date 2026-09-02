import type { CSSProperties } from 'react';
import type { MovementType } from '../api/types';

/**
 * Constantes de estilo compartidas. Viven fuera de los archivos de componentes
 * porque Fast Refresh sólo funciona cuando un archivo exporta únicamente
 * componentes.
 */

export type ChipTone = 'neutral' | 'in' | 'out' | 'adj' | 'danger' | 'warn' | 'acc';

export const CHIP_TONES: Record<ChipTone, CSSProperties> = {
  neutral: { color: 'var(--ink2)', background: 'var(--surf3)', borderColor: 'var(--bord)' },
  in: { color: 'var(--in)', background: 'var(--in-bg)', borderColor: 'var(--in-bord)' },
  out: { color: 'var(--out)', background: 'var(--out-bg)', borderColor: 'var(--out-bord)' },
  adj: { color: 'var(--adj)', background: 'var(--adj-bg)', borderColor: 'var(--adj-bord)' },
  danger: { color: 'var(--dang)', background: 'var(--dang-bg)', borderColor: 'var(--dang-bord)' },
  warn: { color: 'var(--warn)', background: 'var(--warn-bg)', borderColor: 'var(--warn-bord)' },
  acc: { color: 'var(--acc)', background: 'var(--acc-soft)', borderColor: 'var(--acc-ring)' },
};

export type NoteTone = 'neutral' | 'danger' | 'warn' | 'in' | 'adj' | 'acc';

export const NOTE_TONES: Record<NoteTone, CSSProperties> = {
  neutral: { color: 'var(--ink2)', background: 'var(--surf3)', borderColor: 'var(--bord)' },
  danger: { color: 'var(--dang)', background: 'var(--dang-bg)', borderColor: 'var(--dang-bord)' },
  warn: { color: 'var(--warn)', background: 'var(--warn-bg)', borderColor: 'var(--warn-bord)' },
  in: { color: 'var(--in)', background: 'var(--in-bg)', borderColor: 'var(--in-bord)' },
  adj: { color: 'var(--adj)', background: 'var(--adj-bg)', borderColor: 'var(--adj-bord)' },
  acc: { color: 'var(--acc)', background: 'var(--acc-soft)', borderColor: 'var(--acc-ring)' },
};

export type ButtonKind = 'primary' | 'ghost' | 'soft' | 'danger';

export const BUTTON_STYLES: Record<ButtonKind, CSSProperties> = {
  primary: { background: 'var(--acc)', color: 'var(--acc-ink)', borderColor: 'var(--acc)' },
  ghost: { background: 'var(--surf)', color: 'var(--ink)', borderColor: 'var(--bord2)' },
  soft: { background: 'var(--surf3)', color: 'var(--ink2)', borderColor: 'var(--bord)' },
  danger: { background: 'var(--surf)', color: 'var(--dang)', borderColor: 'var(--dang-bord)' },
};

/** Un solo lugar define cómo se ve cada tipo de movimiento. */
export const MOVEMENT: Record<
  MovementType,
  { label: string; tone: ChipTone; icon: string; color: string; bg: string }
> = {
  In: { label: 'Entrada', tone: 'in', icon: 'down', color: 'var(--in)', bg: 'var(--in-bg)' },
  Out: { label: 'Salida', tone: 'out', icon: 'right', color: 'var(--out)', bg: 'var(--out-bg)' },
  Adjustment: {
    label: 'Ajuste', tone: 'adj', icon: 'filter', color: 'var(--adj)', bg: 'var(--adj-bg)',
  },
};
