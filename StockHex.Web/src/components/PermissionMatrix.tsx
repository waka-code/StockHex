import { useMemo, type CSSProperties } from 'react';
import type { PermissionCatalogResponse, PermissionKey } from '../api/types';
import { Icon } from './Icon';

// ════════════════════════════════════════════════ matriz de permisos

/** Casilla de la rejilla. `undefined` = el módulo no tiene esa acción. */
function Cell({
  granted, onToggle, disabled,
}: { granted: boolean | undefined; onToggle?: () => void; disabled?: boolean }) {
  if (granted === undefined) {
    return (
      <span
        aria-hidden
        style={{
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          width: 34, height: 22, color: 'var(--ink3)', opacity: 0.3, fontSize: 12,
        }}
      >
        —
      </span>
    );
  }

  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={granted}
      disabled={disabled}
      onClick={onToggle}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        width: 34, height: 22, padding: 0, borderRadius: 5,
        background: granted ? 'var(--acc)' : 'var(--surf)',
        color: 'var(--acc-ink)',
        border: `1px solid ${granted ? 'var(--acc)' : 'var(--bord2)'}`,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.45 : 1,
      }}
    >
      {granted ? <Icon name="check" size={13} strokeWidth={2.4} /> : null}
    </button>
  );
}

/** Chip para las acciones que no entran en la rejilla de cuatro columnas. */
function SpecialToggle({
  label, granted, onToggle, disabled,
}: { label: string; granted: boolean; onToggle: () => void; disabled?: boolean }) {
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={granted}
      disabled={disabled}
      onClick={onToggle}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: '3px 8px', fontSize: 11, fontWeight: 500, borderRadius: 5,
        background: granted ? 'var(--adj-bg)' : 'var(--surf)',
        color: granted ? 'var(--adj)' : 'var(--ink3)',
        border: `1px solid ${granted ? 'var(--adj-bord)' : 'var(--bord2)'}`,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.45 : 1,
      }}
    >
      <Icon name={granted ? 'check' : 'x'} size={11} strokeWidth={2.2} />
      {label}
    </button>
  );
}

const ICONS: Record<string, string> = {
  dashboard: 'grid', products: 'box', movements: 'swap', categories: 'tag',
  suppliers: 'truck', clients: 'users', reports: 'chart', users: 'shield', roles: 'lock',
};

export function PermissionMatrix({
  catalog, granted, onChange, readOnly,
}: {
  catalog: PermissionCatalogResponse;
  granted: ReadonlySet<PermissionKey>;
  onChange: (next: Set<PermissionKey>) => void;
  readOnly: boolean;
}) {
  const byModule = useMemo(() => catalog.modules.map((module) => {
    const entries = module.permissions
      .map((key) => catalog.permissions.find((p) => p.key === key)!)
      .filter(Boolean);
    return {
      ...module,
      standard: catalog.standardActions.map(
        (action) => entries.find((e) => e.action === action.action),
      ),
      special: entries.filter((e) => e.isSpecial),
      total: entries.length,
    };
  }), [catalog]);

  /**
   * Marcar crear, editar o eliminar arrastra el «ver» del módulo: sin él la
   * pantalla no se puede abrir, así que el permiso sería inalcanzable.
   */
  const toggle = (key: PermissionKey, moduleKey: string) => {
    const next = new Set(granted);

    if (next.has(key)) {
      next.delete(key);
      // Quitar «ver» quita todo el módulo, por el mismo motivo.
      if (key.endsWith('.view')) {
        for (const candidate of catalog.permissions) {
          if (candidate.module === moduleKey) next.delete(candidate.key);
        }
      }
    } else {
      next.add(key);
      const view = catalog.permissions.find(
        (p) => p.module === moduleKey && p.action === 'view',
      );
      if (view) next.add(view.key);
    }

    onChange(next);
  };

  const toggleModule = (keys: PermissionKey[]) => {
    const next = new Set(granted);
    const allOn = keys.every((k) => next.has(k));
    for (const key of keys) {
      if (allOn) next.delete(key);
      else next.add(key);
    }
    onChange(next);
  };

  const th: CSSProperties = {
    padding: '8px 6px', fontSize: 10, fontWeight: 600, color: 'var(--ink2)',
    textTransform: 'uppercase', letterSpacing: '.05em', textAlign: 'center',
    width: 62, background: 'var(--surf2)', borderBottom: '1px solid var(--bord)',
  };

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%' }}>
        <thead>
          <tr>
            <th style={{ ...th, textAlign: 'left', width: 'auto', padding: '8px 12px' }}>Módulo</th>
            {catalog.standardActions.map((action) => (
              <th key={action.action} style={th}>{action.label}</th>
            ))}
            <th style={{ ...th, textAlign: 'left', width: 'auto', padding: '8px 10px' }}>
              Especiales
            </th>
            <th style={{ ...th, textAlign: 'right', width: 56, padding: '8px 12px' }}>Total</th>
          </tr>
        </thead>
        <tbody>
          {byModule.map((module, index) => {
            const count = module.permissions.filter((k) => granted.has(k)).length;
            const complete = count === module.total;
            const border = index === byModule.length - 1
              ? undefined
              : '1px solid var(--bord)';

            return (
              <tr key={module.module}>
                <td style={{ padding: '7px 12px', borderBottom: border }}>
                  <button
                    type="button"
                    disabled={readOnly}
                    onClick={() => toggleModule(module.permissions)}
                    title={readOnly ? undefined : 'Marcar o desmarcar todo el módulo'}
                    style={{
                      display: 'inline-flex', alignItems: 'center', gap: 9,
                      padding: 0, background: 'transparent', border: 0,
                      color: 'inherit', textAlign: 'left',
                      cursor: readOnly ? 'default' : 'pointer',
                    }}
                  >
                    <span style={{ color: 'var(--ink2)' }}>
                      <Icon name={ICONS[module.module] ?? 'lock'} size={15} />
                    </span>
                    <span>
                      <span style={{ fontSize: 12.5, fontWeight: 500 }}>{module.label}</span>
                      <span
                        className="num"
                        style={{
                          display: 'block', fontSize: 10, color: 'var(--ink3)', marginTop: 1,
                        }}
                      >
                        {module.module}.*
                      </span>
                    </span>
                  </button>
                </td>

                {module.standard.map((entry, i) => (
                  <td
                    key={catalog.standardActions[i].action}
                    style={{ padding: '7px 6px', textAlign: 'center', borderBottom: border }}
                  >
                    <Cell
                      granted={entry ? granted.has(entry.key) : undefined}
                      disabled={readOnly}
                      onToggle={entry ? () => toggle(entry.key, module.module) : undefined}
                    />
                  </td>
                ))}

                <td style={{ padding: '7px 10px', borderBottom: border }}>
                  <span style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {module.special.length === 0 ? (
                      <span style={{ color: 'var(--ink3)', opacity: 0.4, fontSize: 11 }}>—</span>
                    ) : module.special.map((entry) => (
                      <SpecialToggle
                        key={entry.key}
                        label={entry.actionLabel}
                        granted={granted.has(entry.key)}
                        disabled={readOnly}
                        onToggle={() => toggle(entry.key, module.module)}
                      />
                    ))}
                  </span>
                </td>

                <td
                  className="num"
                  style={{
                    padding: '7px 12px', textAlign: 'right', fontSize: 11, fontWeight: 500,
                    color: complete ? 'var(--acc)' : 'var(--ink3)', borderBottom: border,
                  }}
                >
                  {count}/{module.total}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

