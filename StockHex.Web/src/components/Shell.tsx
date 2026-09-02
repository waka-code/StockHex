import { useState, type ReactNode } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { Icon } from './Icon';
import { ThemeToggle } from './ThemeToggle';
import { useAuth, useCurrentUser } from '../auth/useAuth';
import { navFor } from '../auth/roles';
import { initials } from '../lib/format';

/**
 * El rol de sistema se distingue visualmente porque es el que no se puede
 * eliminar ni dejar sin permisos. El resto comparte un tono neutro: con roles
 * configurables no se puede tener un color por nombre.
 */
function roleTone(isSystem: boolean) {
  return isSystem
    ? { color: 'var(--acc)', background: 'var(--acc-soft)' }
    : { color: 'var(--adj)', background: 'var(--adj-bg)' };
}

function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const { permissions } = useAuth();
  const items = navFor(permissions);

  return (
    <aside
      style={{
        width: 232, flexShrink: 0, height: '100%',
        background: 'var(--nav-bg)', borderRight: '1px solid var(--nav-bord)',
        display: 'flex', flexDirection: 'column',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '16px 16px 14px' }}>
        <span
          style={{
            width: 26, height: 26, borderRadius: 6, background: 'var(--acc)',
            color: 'var(--acc-ink)', display: 'flex',
            alignItems: 'center', justifyContent: 'center',
          }}
        >
          <Icon name="logo" size={15} strokeWidth={2} />
        </span>
        <span style={{ fontSize: 14, fontWeight: 600, color: '#fff', letterSpacing: '-.02em' }}>
          StockHex
        </span>
      </div>

      <div style={{ height: 1, background: 'var(--nav-bord)', margin: '0 12px 10px' }} />

      <nav style={{ display: 'flex', flexDirection: 'column', gap: 2, padding: '0 10px' }}>
        {items.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            end={item.path === '/'}
            onClick={onNavigate}
            style={({ isActive }) => ({
              position: 'relative',
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '7px 12px', borderRadius: 6,
              fontSize: 13, letterSpacing: '-.01em', textDecoration: 'none',
              fontWeight: isActive ? 500 : 400,
              background: isActive ? 'var(--nav-act)' : 'transparent',
              color: isActive ? '#fff' : 'var(--nav-ink)',
            })}
          >
            {({ isActive }) => (
              <>
                {isActive ? (
                  <span
                    aria-hidden
                    style={{
                      position: 'absolute', left: 0, top: 6, bottom: 6, width: 2,
                      background: 'var(--acc)', borderRadius: '0 2px 2px 0',
                    }}
                  />
                ) : null}
                <Icon name={item.icon} size={16} />
                <span>{item.label}</span>
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <div
        style={{
          marginTop: 'auto', padding: '12px 16px 14px',
          borderTop: '1px solid var(--nav-bord)',
          display: 'flex', alignItems: 'center', gap: 8,
          color: 'var(--nav-ink2)', fontSize: 11,
        }}
      >
        <Icon name="info" size={13} />
        <span>v1.0 · MVP</span>
      </div>
    </aside>
  );
}

interface HeaderProps {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  onMenu: () => void;
}

function Header({ title, subtitle, actions, onMenu }: HeaderProps) {
  const user = useCurrentUser();
  const { logout } = useAuth();
  const tone = roleTone(user.role.isSystem);

  return (
    <header
      style={{
        minHeight: 57, flexShrink: 0, background: 'var(--surf)',
        borderBottom: '1px solid var(--bord)',
        display: 'flex', alignItems: 'center', gap: 14, padding: '8px 20px',
      }}
    >
      <button
        type="button"
        onClick={onMenu}
        aria-label="Abrir menú"
        className="shx-menu-button"
        style={{
          display: 'none', alignItems: 'center', justifyContent: 'center',
          width: 30, height: 30, background: 'transparent',
          border: '1px solid var(--bord)', borderRadius: 'var(--r)',
          color: 'var(--ink2)', cursor: 'pointer',
        }}
      >
        <Icon name="filter" size={15} />
      </button>

      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 15, fontWeight: 600, letterSpacing: '-.02em' }}>{title}</div>
        {subtitle ? (
          <div style={{ fontSize: 12, color: 'var(--ink3)', marginTop: 1 }}>{subtitle}</div>
        ) : null}
      </div>

      <div
        style={{
          marginLeft: 'auto', display: 'flex', alignItems: 'center',
          gap: 10, flexWrap: 'wrap', justifyContent: 'flex-end',
        }}
      >
        {actions}
        <ThemeToggle />
        <div style={{ width: 1, height: 24, background: 'var(--bord)' }} />
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <span
            aria-hidden
            style={{
              width: 28, height: 28, borderRadius: '50%',
              background: 'var(--surf3)', border: '1px solid var(--bord)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 11, fontWeight: 600, color: 'var(--ink2)',
            }}
          >
            {initials(user.name)}
          </span>
          <div>
            <div style={{ fontSize: 12, fontWeight: 500, lineHeight: 1.3 }}>{user.name}</div>
            <span
              style={{
                display: 'inline-block', marginTop: 1, padding: '1px 5px',
                fontSize: 10, fontWeight: 500, borderRadius: 3,
                letterSpacing: '.02em', ...tone,
              }}
              title={user.role.isSystem ? 'Rol de sistema' : 'Rol personalizado'}
            >
              {user.role.name}
            </span>
          </div>
        </div>
        <button
          type="button"
          onClick={() => void logout()}
          title="Cerrar sesión"
          aria-label="Cerrar sesión"
          style={{
            display: 'flex', padding: 6, background: 'transparent',
            border: 0, borderRadius: 'var(--r)',
            color: 'var(--ink2)', cursor: 'pointer', opacity: 0.6,
          }}
        >
          <Icon name="logout" size={16} />
        </button>
      </div>
    </header>
  );
}

/**
 * Cada página declara su título y acciones a través de este contexto en lugar de
 * repetir la cabecera, para que el cascarón no se remonte al navegar.
 */
export interface PageMeta {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}

export function Shell() {
  const [meta, setMeta] = useState<PageMeta>({ title: 'StockHex' });
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: '100vh' }}>
      <div className="shx-sidebar" data-open={menuOpen || undefined}>
        <Sidebar onNavigate={() => setMenuOpen(false)} />
      </div>

      {menuOpen ? (
        <div
          role="presentation"
          onClick={() => setMenuOpen(false)}
          className="shx-scrim"
        />
      ) : null}

      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Header {...meta} onMenu={() => setMenuOpen(true)} />
        <main
          key={location.pathname}
          style={{
            flex: 1, padding: 20, display: 'flex', flexDirection: 'column', gap: 16,
            animation: 'shx-fade .16s ease-out',
          }}
        >
          <Outlet context={{ setMeta }} />
        </main>
      </div>
    </div>
  );
}
