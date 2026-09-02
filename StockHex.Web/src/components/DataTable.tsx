import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { Icon } from './Icon';
import { EmptyState, Spinner } from './ui';
import { PAGE_SIZES, type PagedResponse, type PageSize } from '../api/types';

export interface Column<T> {
  /**
   * La última columna con la clave `actions` queda **fijada a la derecha**: cuando
   * la tabla no cabe y hay que desplazarla, la acción principal de la fila tiene
   * que seguir alcanzable sin descubrir un scroll horizontal. Las siete tablas del
   * proyecto ya la nombran así.
   */
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

/**
 * Ancho mínimo de una columna sin `width` declarado. Las flexibles son siempre
 * las de nombre, producto o descripción, y son las que absorben el espacio
 * sobrante cuando la tabla cabe holgada.
 */
const FLEXIBLE_COLUMN_MIN = 160;

export function DataTable<T>({
  columns, rows, rowKey, loading, empty, rowTone, onRowClick,
}: Props<T>) {
  // El `width` de cada columna es una sugerencia que el navegador ignora en
  // cuanto la tabla no cabe: con `width: 100%` y sin mínimo, en vez de
  // desbordar comprime las columnas y parte el texto en tres líneas, dejando
  // filas de más de cien píxeles de alto. Sumando los anchos declarados la
  // tabla sí desborda, y el contenedor de arriba —que ya pedía `overflowX:
  // auto`— por fin tiene algo que desplazar.
  const minWidth = columns.reduce(
    (total, column) => total + (column.width ?? FLEXIBLE_COLUMN_MIN),
    0,
  );

  // Con la tabla desplazándose, la columna de acciones sería lo primero en salir
  // de la vista justo por ser la última, y es la que lleva el botón de la fila.
  const pinnedKey = columns[columns.length - 1]?.key === 'actions' ? 'actions' : null;

  // El separador de la columna fijada sólo aparece cuando hay algo pasando por
  // debajo. En una tabla que cabe entera sería una línea de más, y la mayoría
  // caben: sólo Movimientos y Productos desbordan a anchos de portátil.
  const scroller = useRef<HTMLDivElement>(null);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const node = scroller.current;
    if (!node) return;

    const update = () => setScrolled(node.scrollLeft > 0);
    update();

    node.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    return () => {
      node.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
    };
  }, [rows.length]);

  const pin = (isPinned: boolean, background: string): CSSProperties => (isPinned
    ? {
      position: 'sticky',
      right: 0,
      background,
      borderLeft: scrolled ? '1px solid var(--bord)' : '1px solid transparent',
    }
    : {});

  if (loading) return <Spinner label="Cargando…" />;

  if (rows.length === 0) {
    return <>{empty ?? <EmptyState title="Sin resultados" detail="No hay nada que mostrar todavía." />}</>;
  }

  return (
    <div ref={scroller} style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%', minWidth }}>
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
                  // La cabecera fijada compone los dos ejes: `top` la mantiene
                  // arriba y `right`, si es la columna fijada, a la derecha.
                  position: 'sticky', top: 0, zIndex: column.key === pinnedKey ? 2 : 1,
                  ...pin(column.key === pinnedKey, 'var(--surf2)'),
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
                    // El fondo tiene que ser opaco para que el contenido que pasa
                    // por debajo al desplazar no se transparente; se respeta el
                    // tono de la fila cuando lo hay.
                    ...pin(column.key === pinnedKey, rowTone?.(row) ?? 'var(--surf)'),
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
