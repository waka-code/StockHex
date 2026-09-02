import { useQuery } from '@tanstack/react-query';
import { permissions as permissionsApi } from '../api/endpoints';

/**
 * El catálogo se pide una vez y se cachea de por vida: no cambia sin un
 * despliegue, porque vive en el código del backend (regla 7 de CLAUDE.md).
 *
 * Vive fuera de PermissionMatrix.tsx porque Fast Refresh sólo funciona cuando un
 * archivo exporta únicamente componentes.
 */
export function usePermissionCatalog() {
  return useQuery({
    queryKey: ['permissions', 'catalog'],
    queryFn: () => permissionsApi.catalog(),
    staleTime: Infinity,
    gcTime: Infinity,
  });
}
