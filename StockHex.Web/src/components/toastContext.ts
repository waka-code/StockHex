import { createContext } from 'react';

export interface ToastApi {
  success: (title: string, detail?: string) => void;
  error: (title: string, detail?: string) => void;
  warn: (title: string, detail?: string) => void;
  /** Traduce un error de la API al aviso que corresponde a su status. */
  fromError: (error: unknown, fallbackTitle?: string) => void;
}

export const ToastContext = createContext<ToastApi | null>(null);
