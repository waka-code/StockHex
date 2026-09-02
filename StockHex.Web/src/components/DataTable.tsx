import type { ReactNode } from 'react';
import { Icon } from './Icon';
import { EmptyState, Spinner } from './ui';
import { PAGE_SIZES, type PagedResponse, type PageSize } from '../api/types';

export interface Column<T> {
  key: string;
  header: ReactNode;
  align?: 'left' | 'right' | 'center';
  width?: number;
  render: (row: T) => ReactNode;
}

interface Props<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  loading?: boolean;
  /** Se muestra cuando no hay filas. */
  empty?: ReactNode;
  /** Fondo distinto para filas que hay que destacar (por ejemplo, reversiones). */
  rowTone?: (row: T) => string | undefined;
  onRowClick?: (row: T) => void;
}

export function DataTable<T>({
  columns, rows, rowKey, loading, empty, rowTone, onRowClick,
}: Props<T>) {
  if (loading) return <Spinner label="Cargando…" />;

  if (rows.length === 0) {
    return <>{empty ?? <EmptyState title="Sin resultados" detail="No hay nada que mostrar todavía." />}</>;
  }

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%' }}>
        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                style={{
                  padding: '8px 12px', fontSize: 10.5, fontWeight: 600,
                  color: 'var(--ink2)', textTransform: 'uppercase',
                  letterSpacing: '.06em', textAlign: column.align ?? 'left',
                  width: column.width, whiteSpace: 'nowrap',
                  background: 'var(--surf2)', borderBottom: '1px solid var(--bord)',
                  position: 'sticky', top: 0, zIndex: 1,
                }}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr
              key={rowKey(row)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              style={{
                background: rowTone?.(row),
                cursor: onRowClick ? 'pointer' : undefined,
              }}
            >
              {columns.map((column) => (
                <td
                  key={column.key}
                  style={{
                    padding: '9px 12px', fontSize: 12.5, verticalAlign: 'middle',
                    textAlign: column.align ?? 'left',
                    borderBottom: index === rows.length - 1 ? undefined : '1px solid var(--bord)',
                  }}
                >
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ───────────────────────────────────────────────────── paginación

function PageButton({
  children, onClick, active, disabled, label,
}: {
  children: ReactNode;
  onClick?: () => void;
  active?: boolean;
  disabled?: boolean;
  label?: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      aria-current={active ? 'page' : undefined}
      style={{
        minWidth: 26, height: 26, padding: '0 6px',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 12, fontWeight: 500,
        background: active ? 'var(--acc)' : 'var(--surf)',
        color: active ? 'var(--acc-ink)' : disabled ? 'var(--ink3)' : 'var(--ink)',
        border: `1px solid ${active ? 'var(--acc)' : 'var(--bord2)'}`,
        borderRadius: 5,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.5 : 1,
      }}
    >
      {children}
    </button>
  );
}

/** Ventana de páginas alrededor de la actual, para no pintar 200 botones. */
function windowOf(page: number, total: number): number[] {
  if (total <= 5) return Array.from({ length: total }, (_, i) => i + 1);
  const start = Math.max(1, Math.min(page - 2, total - 4));
  return Array.from({ length: 5 }, (_, i) => start + i);
}

/**
 * Paginación de una tabla. `pageSize` y `onPageSize` son opcionales: si no se
 * pasan, la barra no ofrece el selector de filas.
 *
 * El tamaño elegido no se guarda aquí: quien la usa lo escribe en la URL con
 * `useUrlFilters`, y el listado se vuelve a pedir a la API con el nuevo
 * `pageSize`. Esta barra no recorta nada en memoria.
 */
export function Pager<T>({
  data, onPage, pageSize, onPageSize,
}: {
  data: Pick<PagedResponse<T>, 'page' | 'pageSize' | 'totalCount' | 'totalPages' | 'hasPrevious' | 'hasNext'>;
  onPage: (page: number) => void;
  pageSize?: PageSize;
  onPageSize?: (size: PageSize) => void;
}) {
  const { page, totalCount, totalPages, hasPrevious, hasNext } = data;
  const pages = windowOf(page, totalPages);

  return (
    <div
      style={{
        display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap',
        padding: '11px 14px', background: 'var(--surf2)',
        borderTop: '1px solid var(--bord)',
      }}
    >
      <span style={{ fontSize: 11.5, color: 'var(--ink2)' }}>
        {totalCount === 0 ? 'Sin registros' : (
          <>
            Página <span className="num" style={{ color: 'var(--ink)', fontWeight: 500 }}>{page}</span>
            {' de '}
            <span className="num" style={{ color: 'var(--ink)', fontWeight: 500 }}>{totalPages}</span>
            {' · '}
            <span className="num" style={{ color: 'var(--ink)', fontWeight: 500 }}>{totalCount}</span>
            {totalCount === 1 ? ' registro' : ' registros'}
          </>
        )}
      </span>

      {pageSize !== undefined && onPageSize ? (
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11.5, color: 'var(--ink2)' }}>
          Mostrar
          <select
            value={pageSize}
            onChange={(event) => onPageSize(Number(event.target.value) as PageSize)}
            aria-label="Filas por página"
            style={{
              height: 26, padding: '0 6px', fontSize: 12, fontWeight: 500,
              background: 'var(--surf)', color: 'var(--ink)',
              border: '1px solid var(--bord2)', borderRadius: 5, cursor: 'pointer',
            }}
          >
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>{size}</option>
            ))}
          </select>
          por página
        </label>
      ) : null}

      {totalPages > 1 ? (
        <span style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 5 }}>
          <PageButton onClick={() => onPage(page - 1)} disabled={!hasPrevious} label="Página anterior">
            <Icon name="left" size={13} />
          </PageButton>
          {pages[0] > 1 ? (
            <span style={{ color: 'var(--ink3)', fontSize: 12, padding: '0 3px' }}>…</span>
          ) : null}
          {pages.map((p) => (
            <PageButton key={p} onClick={() => onPage(p)} active={p === page} label={`Página ${p}`}>
              {p}
            </PageButton>
          ))}
          {pages[pages.length - 1] < totalPages ? (
            <span style={{ color: 'var(--ink3)', fontSize: 12, padding: '0 3px' }}>…</span>
          ) : null}
          <PageButton onClick={() => onPage(page + 1)} disabled={!hasNext} label="Página siguiente">
            <Icon name="right" size={13} />
          </PageButton>
        </span>
      ) : null}
    </div>
  );
}
