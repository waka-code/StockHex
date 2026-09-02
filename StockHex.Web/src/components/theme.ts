export type Theme = 'light' | 'dark' | 'system';

const KEY = 'stockhex.theme';

export function readTheme(): Theme {
  try {
    const stored = localStorage.getItem(KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    return 'system';
  }
}

export function applyTheme(theme: Theme): void {
  const root = document.documentElement;
  // Sin atributo se respeta prefers-color-scheme, que es lo que hace el CSS.
  if (theme === 'system') root.removeAttribute('data-theme');
  else root.setAttribute('data-theme', theme);
}

export function storeTheme(theme: Theme): void {
  try {
    if (theme === 'system') localStorage.removeItem(KEY);
    else localStorage.setItem(KEY, theme);
  } catch {
    // Modo privado: el tema dura sólo la sesión.
  }
}

/** Se llama antes de montar React para que no haya un destello del tema claro. */
export function initTheme(): void {
  applyTheme(readTheme());
}
