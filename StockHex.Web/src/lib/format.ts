/** Pesos chilenos: sin decimales y con punto como separador de miles. */
const clpFormatter = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0,
});

export function clp(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return clpFormatter.format(value);
}

/** Compacto para tarjetas estrechas: $1,87M en lugar de $1.875.410. */
export function clpCompact(value: number): string {
  if (Math.abs(value) >= 1_000_000) {
    return `$${(value / 1_000_000).toLocaleString('es-CL', { maximumFractionDigits: 2 })}M`;
  }
  if (Math.abs(value) >= 10_000) {
    return `$${Math.round(value / 1_000).toLocaleString('es-CL')}K`;
  }
  return clp(value);
}

const numberFormatter = new Intl.NumberFormat('es-CL');

export function num(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return numberFormatter.format(value);
}

/**
 * La API entrega fechas UTC. Se convierten a la zona del navegador, porque el
 * usuario razona en su hora local, no en UTC.
 */
function toDate(iso: string | null | undefined): Date | null {
  if (!iso) return null;
  // Las fechas de .NET pueden llegar sin sufijo de zona; se asumen UTC.
  const normalized = /[Z+]|-\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`;
  const date = new Date(normalized);
  return Number.isNaN(date.getTime()) ? null : date;
}

/**
 * Reloj de 24 horas siempre. es-CL usa 12 horas con "a. m." / "p. m.", que en una
 * columna estrecha se parte en dos líneas y rompe la alineación de la tabla.
 */
export function dateTime(iso: string | null | undefined): string {
  const date = toDate(iso);
  if (!date) return '—';
  return date.toLocaleString('es-CL', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  });
}

/** Compacto para tablas densas: "02-09 10:42". */
export function dateTimeShort(iso: string | null | undefined): string {
  const date = toDate(iso);
  if (!date) return '—';

  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');

  return `${day}-${month} ${hours}:${minutes}`;
}

export function dateOnly(iso: string | null | undefined): string {
  const date = toDate(iso);
  if (!date) return '—';
  return date.toLocaleDateString('es-CL', {
    day: '2-digit', month: '2-digit', year: 'numeric',
  });
}

export function relative(iso: string | null | undefined): string {
  const date = toDate(iso);
  if (!date) return 'nunca';

  const minutes = Math.round((Date.now() - date.getTime()) / 60_000);
  if (minutes < 1) return 'ahora';
  if (minutes < 60) return `hace ${minutes} min`;
  if (minutes < 60 * 24) return `hace ${Math.round(minutes / 60)} h`;
  if (minutes < 60 * 24 * 7) return `hace ${Math.round(minutes / (60 * 24))} d`;
  return dateOnly(iso);
}

/** Para <input type="date">, que exige exactamente aaaa-mm-dd. */
export function toDateInput(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function initials(name: string): string {
  return name.trim().split(/\s+/).slice(0, 2).map((p) => p[0] ?? '').join('').toUpperCase();
}
