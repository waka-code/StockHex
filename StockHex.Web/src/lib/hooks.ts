import { useEffect, useRef, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import type { PageMeta } from '../components/Shell';

interface ShellContext {
  setMeta: (meta: PageMeta) => void;
}

/**
 * Cada página declara su cabecera. Las dependencias se comparan por valor
 * serializado porque `actions` es JSX nuevo en cada render: comparar por
 * identidad provocaría un bucle infinito de actualizaciones.
 */
export function usePageMeta(meta: PageMeta, deps: unknown[] = []): void {
  const { setMeta } = useOutletContext<ShellContext>();
  const key = JSON.stringify([meta.title, meta.subtitle, ...deps]);

  useEffect(() => {
    setMeta(meta);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
}

/** Retrasa el valor para no disparar una consulta por cada tecla. */
export function useDebounced<T>(value: T, delay = 350): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delay);
    return () => window.clearTimeout(timer);
  }, [value, delay]);

  return debounced;
}

/** Vuelve a la página 1 cuando cambia un filtro: la 7 puede no existir ya. */
export function useResetPageOnFilterChange(
  filterKey: string,
  page: number,
  setPage: (page: number) => void,
): void {
  const previous = useRef(filterKey);

  useEffect(() => {
    if (previous.current !== filterKey) {
      previous.current = filterKey;
      if (page !== 1) setPage(1);
    }
  }, [filterKey, page, setPage]);
}
