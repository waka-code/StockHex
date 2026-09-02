import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { DEFAULT_PAGE_SIZE, PAGE_SIZES, type PageSize } from '../api/types';

/**
 * Filtros derivados de la URL (regla 4 de CLAUDE.md).
 *
 * El estado NO se duplica: la URL es la única fuente y los valores se leen de
 * ella en cada render. Así refrescar conserva la consulta y copiar el enlace
 * reconstruye la pantalla.
 *
 * Los valores por defecto no se escriben en la URL: `?page=1&pageSize=15` es
 * ruido y hace ilegible un enlace que se comparte.
 */
export interface ParamDef<T> {
  parse(raw: string | null): T;
  /** `null` omite el parámetro de la URL. */
  serialize(value: T): string | null;
  isDefault(value: T): boolean;
  /**
   * True sólo para `page`: moverse de página no reinicia la paginación.
   * Cualquier otro parámetro sí la reinicia, porque la página 7 puede no existir
   * con el filtro nuevo — y `pageSize` es de los que la reinician: la página 7
   * de 10 en 10 no tiene equivalente de 25 en 25.
   */
  isPagination: boolean;
}

// ───────────────────────────────────────────── definiciones de parámetro

export function stringParam(fallback = ''): ParamDef<string> {
  return {
    parse: (raw) => raw ?? fallback,
    serialize: (value) => (value === fallback ? null : value),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

export function numberParam(
  fallback: number,
  options: { min?: number; max?: number; pagination?: boolean } = {},
): ParamDef<number> {
  const { min, max, pagination = false } = options;

  const clamp = (value: number) => {
    if (min !== undefined && value < min) return min;
    if (max !== undefined && value > max) return max;
    return value;
  };

  return {
    parse: (raw) => {
      if (raw === null) return fallback;
      const parsed = Number(raw);
      // Un valor basura en la URL no debe romper la pantalla: se cae al defecto.
      return Number.isFinite(parsed) ? clamp(Math.trunc(parsed)) : fallback;
    },
    serialize: (value) => (value === fallback ? null : String(value)),
    isDefault: (value) => value === fallback,
    isPagination: pagination,
  };
}

/**
 * Tamaño de página. Sólo acepta los valores que el backend ofrece
 * (`PAGE_SIZES`): un `?pageSize=99999` escrito a mano cae al defecto en vez de
 * pedirle a la API un listado que no va a servir.
 *
 * No es `pagination`: cambiar cuántas filas caben vuelve a la página 1.
 */
export function pageSizeParam(fallback: PageSize = DEFAULT_PAGE_SIZE): ParamDef<PageSize> {
  const allowed = (value: number): value is PageSize =>
    (PAGE_SIZES as readonly number[]).includes(value);

  return {
    parse: (raw) => {
      if (raw === null) return fallback;
      const parsed = Number(raw);
      return allowed(parsed) ? parsed : fallback;
    },
    serialize: (value) => (value === fallback ? null : String(value)),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

export function boolParam(fallback = false): ParamDef<boolean> {
  return {
    parse: (raw) => (raw === null ? fallback : raw === 'true' || raw === '1'),
    serialize: (value) => (value === fallback ? null : String(value)),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

/** Enumerado cerrado: un valor no admitido en la URL cae al defecto. */
export function enumParam<T extends string>(
  allowed: readonly T[],
  fallback: T,
): ParamDef<T> {
  return {
    parse: (raw) => (raw !== null && (allowed as readonly string[]).includes(raw)
      ? (raw as T)
      : fallback),
    serialize: (value) => (value === fallback ? null : value),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

/** Fecha en formato aaaa-mm-dd, el que usa `<input type="date">`. */
export function dateParam(fallback = ''): ParamDef<string> {
  const isValid = (raw: string) => /^\d{4}-\d{2}-\d{2}$/.test(raw);

  return {
    parse: (raw) => (raw !== null && isValid(raw) ? raw : fallback),
    serialize: (value) => (value === fallback || !isValid(value) ? null : value),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

/**
 * Identificador de una entidad: un GUID, el formato que devuelve la API.
 *
 * Existe porque `stringParam()` deja pasar cualquier cosa, y un `?categoryId=`
 * corrupto —un enlace mal copiado, una URL editada a mano— viajaba tal cual a la
 * API, que responde `400` y deja la pantalla con un aviso de error en vez del
 * listado. La regla 4 pide parseo tolerante: lo que no tiene forma de id se
 * descarta y el filtro vuelve a «todos».
 */
export function guidParam(fallback = ''): ParamDef<string> {
  const isValid = (raw: string) =>
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(raw);

  return {
    parse: (raw) => (raw !== null && isValid(raw) ? raw : fallback),
    serialize: (value) => (value === fallback || !isValid(value) ? null : value),
    isDefault: (value) => value === fallback,
    isPagination: false,
  };
}

// ───────────────────────────────────────────────────────────── el hook

/**
 * `ParamDef<unknown>` como cota, no `ParamDef<never>`: `parse` devuelve T, que
 * está en posición covariante, así que sólo `unknown` acepta cualquier T. La
 * inferencia conserva el tipo exacto de cada clave, y `ValueOf` lo recupera.
 */
type Schema = Record<string, ParamDef<unknown>>;

type ValueOf<D> = D extends ParamDef<infer T> ? T : never;

/**
 * Vista sin tipo de un parámetro, para el interior del hook. Los métodos de
 * `ParamDef<T>` son bivariantes en TypeScript, así que cualquier `ParamDef<T>`
 * encaja aquí sin necesidad de `any` ni de silenciar el compilador.
 */
interface ErasedParam {
  parse(raw: string | null): unknown;
  serialize(value: unknown): string | null;
  isDefault(value: unknown): boolean;
  isPagination: boolean;
}

export type FilterValues<S extends Schema> = { [K in keyof S]: ValueOf<S[K]> };

export interface UrlFilters<S extends Schema> {
  /** Valores actuales, leídos de la URL. */
  values: FilterValues<S> & Record<string, unknown>;
  /** Cambia un filtro. Reinicia la página salvo que el parámetro sea de paginación. */
  set<K extends keyof S>(key: K, value: ValueOf<S[K]>): void;
  /** Cambia varios de una vez, con una sola entrada de historial. */
  setMany(patch: Partial<FilterValues<S>>): void;
  /** Vuelve todo a su valor por defecto. */
  reset(): void;
  /** True si algún filtro que no sea de paginación está fuera de su defecto. */
  isFiltered: boolean;
}

export function useUrlFilters<S extends Schema>(schema: S): UrlFilters<S> {
  const [params, setParams] = useSearchParams();

  // El objeto literal del esquema es nuevo en cada render, pero su contenido no
  // cambia. `useState` con inicializador lo congela para la vida del componente
  // y, al contrario que un ref, se puede leer durante el render.
  const [defs] = useState<Record<string, ErasedParam>>(() => schema);
  const keys = useMemo(() => Object.keys(defs), [defs]);

  const values = useMemo(() => {
    const parsed: Record<string, unknown> = {};
    for (const key of keys) {
      parsed[key] = defs[key].parse(params.get(key));
    }

    // Única conversión del hook, y es sólida: `parsed` se construyó recorriendo
    // las claves del esquema y cada valor salió del `parse` de su propio
    // parámetro, que es justo lo que `FilterValues<S>` describe. Es el puente
    // entre los internos sin tipo y la API pública tipada.
    return parsed as FilterValues<S>;
  }, [params, keys, defs]);

  const write = useCallback((entries: [string, unknown][], resetPage: boolean) => {
    setParams((current) => {
      const next = new URLSearchParams(current);

      for (const [key, value] of entries) {
        const def = defs[key];
        if (!def) continue;

        const serialized = def.serialize(value);
        if (serialized === null) next.delete(key);
        else next.set(key, serialized);
      }

      // Cambiar un filtro devuelve a la página 1: la 7 puede no existir con el
      // filtro nuevo. Borrarla en lugar de escribir `page=1` mantiene la URL corta.
      if (resetPage) next.delete('page');

      return next;
      // `replace` para que el botón atrás navegue entre pantallas y no entre
      // cada ajuste de filtro.
    }, { replace: true });
  }, [setParams, defs]);

  const set = useCallback(<K extends keyof S>(key: K, value: ValueOf<S[K]>) => {
    const name = String(key);
    write([[name, value]], !defs[name].isPagination);
  }, [write, defs]);

  const setMany = useCallback((patch: Partial<FilterValues<S>>) => {
    const entries = Object.entries(patch);
    const touchesFilter = entries.some(([key]) => defs[key] && !defs[key].isPagination);
    write(entries, touchesFilter);
  }, [write, defs]);

  const reset = useCallback(() => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      // Sólo se borran las claves del esquema: si la ruta lleva otros parámetros
      // (por ejemplo, uno de seguimiento), se conservan.
      for (const key of keys) next.delete(key);
      return next;
    }, { replace: true });
  }, [setParams, keys]);

  const isFiltered = useMemo(
    () => keys.some((key) => {
      const def = defs[key];
      return !def.isPagination && !def.isDefault(values[key]);
    }),
    [values, keys, defs],
  );

  return { values, set, setMany, reset, isFiltered };
}

/**
 * Campo de texto cuyo valor vive en la URL pero que no la reescribe en cada
 * tecla. El borrador es estado local (es de la UI, no de la consulta) y se
 * confirma con retardo.
 *
 * Resuelve la única tensión real de la regla 4: derivar de la URL y a la vez no
 * llenar el historial ni disparar una consulta por pulsación.
 */
export function useDebouncedParam(
  committed: string,
  commit: (value: string) => void,
  delay = 350,
): [string, (value: string) => void] {
  const [draft, setDraft] = useState(committed);
  const [seen, setSeen] = useState(committed);

  // Ajustar estado durante el render cuando cambia una entrada externa es el
  // patrón que documenta React, y evita el parpadeo de hacerlo en un efecto:
  // si el valor cambia por fuera (botón atrás, «limpiar filtros», un enlace
  // pegado), el borrador se pone al día antes de pintar.
  if (seen !== committed) {
    setSeen(committed);
    setDraft(committed);
  }

  useEffect(() => {
    if (draft === committed) return;

    const timer = window.setTimeout(() => commit(draft), delay);
    return () => window.clearTimeout(timer);
  }, [draft, committed, commit, delay]);

  return [draft, setDraft];
}
