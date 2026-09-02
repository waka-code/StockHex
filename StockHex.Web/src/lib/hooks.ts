import { useEffect } from 'react';
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
