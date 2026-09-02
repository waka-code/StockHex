import { useContext } from 'react';
import { ToastContext, type ToastApi } from './toastContext';

/**
 * Vive fuera de Toast.tsx porque Fast Refresh sólo funciona cuando un archivo
 * exporta únicamente componentes.
 */
export function useToast(): ToastApi {
  const context = useContext(ToastContext);
  if (!context) throw new Error('useToast debe usarse dentro de <ToastProvider>.');
  return context;
}
