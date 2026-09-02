import { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { applyTheme, readTheme, storeTheme, type Theme } from './theme';

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(readTheme);

  useEffect(() => {
    applyTheme(theme);
    storeTheme(theme);
  }, [theme]);

  const systemIsDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  const showingDark = theme === 'dark' || (theme === 'system' && systemIsDark);

  const next: Theme = showingDark ? 'light' : 'dark';
  const label = showingDark ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro';

  return (
    <button
      type="button"
      onClick={() => setTheme(next)}
      title={label}
      aria-label={label}
      style={{
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        width: 30, height: 30, padding: 0,
        background: 'transparent', border: '1px solid var(--bord)',
        borderRadius: 'var(--r)', color: 'var(--ink2)', cursor: 'pointer',
      }}
    >
      <Icon name={showingDark ? 'sun' : 'moon'} size={15} />
    </button>
  );
}
