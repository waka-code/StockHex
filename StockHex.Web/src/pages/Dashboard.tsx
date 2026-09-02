import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { movements, reports } from '../api/endpoints';
import { Card, CardHead, Chip, EmptyState, Kpi, MovementChip, MovementQuantity, Spinner } from '../components/ui';
import { DataTable, type Column } from '../components/DataTable';
import { Icon } from '../components/Icon';
import { clp, dateTime, dateTimeShort, num } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import { NewMovementButton } from './MovementForm';
import type { LowStockItemResponse, MovementResponse } from '../api/types';

const TODAY = new Date().toLocaleDateString('es-CL', {
  weekday: 'long', day: 'numeric', month: 'long',
});

export function Dashboard() {
  usePageMeta({
    title: 'Dashboard',
    subtitle: TODAY.charAt(0).toUpperCase() + TODAY.slice(1),
    actions: <NewMovementButton />,
  });

  const summary = useQuery({
    queryKey: ['reports', 'inventory-summary'],
    queryFn: () => reports.inventorySummary(),
  });

  const lowStock = useQuery({
    queryKey: ['reports', 'low-stock', { page: 1, pageSize: 6 }],
    queryFn: () => reports.lowStock({ page: 1, pageSize: 6 }),
  });

  const recent = useQuery({
    queryKey: ['movements', { page: 1, pageSize: 8 }],
    queryFn: () => movements.list({ page: 1, pageSize: 8 }),
  });

  const lowColumns: Column<LowStockItemResponse>[] = [
    {
      key: 'sku', header: 'SKU', width: 96,
      render: (row) => <span className="num" style={{ color: 'var(--ink2)' }}>{row.sku}</span>,
    },
    {
      key: 'name', header: 'Producto',
      render: (row) => (
        <>
          <Link to={`/productos/${row.productId}`} style={{ fontWeight: 500, color: 'var(--ink)' }}>
            {row.name}
          </Link>
          {row.categoryName ? (
            <div style={{ fontSize: 11, color: 'var(--ink3)', marginTop: 1 }}>{row.categoryName}</div>
          ) : null}
        </>
      ),
    },
    {
      key: 'stock', header: 'Stock', align: 'right', width: 66,
      render: (row) => (
        <span className="num" style={{ color: 'var(--dang)', fontWeight: 500 }}>
          {row.stockQuantity}
        </span>
      ),
    },
    {
      key: 'min', header: 'Mínimo', align: 'right', width: 66,
      render: (row) => <span className="num" style={{ color: 'var(--ink2)' }}>{row.minimumStock}</span>,
    },
    {
      key: 'deficit', header: 'Déficit', width: 112,
      render: (row) => (row.deficit > 0
        ? <Chip tone="danger">faltan {row.deficit}</Chip>
        : <Chip tone="warn">en el límite</Chip>),
    },
  ];

  const movementColumns: Column<MovementResponse>[] = [
    { key: 'type', header: 'Tipo', width: 96, render: (row) => <MovementChip type={row.movementType} /> },
    {
      key: 'product', header: 'Producto',
      render: (row) => (
        <>
          <Link to={`/productos/${row.productId}`} style={{ fontWeight: 500, color: 'var(--ink)' }}>
            {row.productName ?? '—'}
          </Link>
          <div className="num" style={{ fontSize: 11, color: 'var(--ink3)', marginTop: 1 }}>
            {row.productSku}
          </div>
        </>
      ),
    },
    {
      key: 'qty', header: 'Cant.', align: 'right', width: 64,
      render: (row) => <MovementQuantity type={row.movementType} quantity={row.quantity} />,
    },
    {
      key: 'stock', header: 'Stock', width: 96,
      render: (row) => (
        <span className="num">
          <span style={{ color: 'var(--ink3)' }}>{row.stockBefore}</span>
          <span style={{ color: 'var(--ink3)' }}> → </span>
          <span style={{ fontWeight: 500 }}>{row.stockAfter}</span>
        </span>
      ),
    },
    {
      key: 'party', header: 'Contraparte', width: 158,
      render: (row) => (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12, color: 'var(--ink2)' }}>
            {row.clientName ?? row.supplierName ?? '—'}
          </span>
          {row.reversalOfMovementId ? <Chip tone="adj" icon="undo">reversión</Chip> : null}
        </span>
      ),
    },
    {
      key: 'user', header: 'Usuario', width: 116,
      render: (row) => <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.userName ?? '—'}</span>,
    },
    {
      key: 'date', header: 'Fecha', width: 108,
      render: (row) => (
        <span
          className="num"
          style={{ color: 'var(--ink3)', whiteSpace: 'nowrap' }}
          title={dateTime(row.movementDate)}
        >
          {dateTimeShort(row.movementDate)}
        </span>
      ),
    },
  ];

  const s = summary.data;

  return (
    <>
      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        {summary.isLoading || !s ? (
          <Card style={{ flex: 1 }}><Spinner /></Card>
        ) : (
          <>
            <Kpi
              label="Productos"
              value={num(s.totalProducts)}
              icon="box"
              foot={
                <>
                  <span style={{ color: 'var(--in)' }}><Icon name="check" size={12} /></span>
                  {num(s.activeProducts)} activos
                  {s.totalProducts - s.activeProducts > 0
                    ? ` · ${num(s.totalProducts - s.activeProducts)} inactivos`
                    : null}
                </>
              }
            />
            <Kpi
              label="En stock bajo"
              value={num(s.lowStockProducts)}
              tone={s.lowStockProducts > 0 ? 'var(--dang)' : undefined}
              icon="alert"
              foot={s.lowStockProducts > 0 ? 'requieren reposición' : 'todo sobre el mínimo'}
            />
            <Kpi
              label="Valorización"
              value={clp(s.totalStockValue)}
              icon="chart"
              foot="precio × stock, productos activos"
            />
            <Kpi
              label="Movimientos"
              value={num(recent.data?.totalCount ?? 0)}
              icon="swap"
              foot={recent.data?.items[0]
                ? `último ${dateTimeShort(recent.data.items[0].movementDate)}`
                : 'sin movimientos'}
            />
          </>
        )}
      </div>

      <Card pad={false}>
        <CardHead
          title="Stock bajo"
          sub="Ordenado por déficit respecto del mínimo"
          right={<Link to="/reportes" style={{ fontSize: 12 }}>Ver reporte completo</Link>}
        />
        <DataTable
          columns={lowColumns}
          rows={lowStock.data?.items ?? []}
          rowKey={(row) => row.productId}
          loading={lowStock.isLoading}
          empty={(
            <EmptyState
              icon="check"
              title="Ningún producto bajo su mínimo"
              detail="Todo el catálogo activo está sobre el stock mínimo configurado."
            />
          )}
        />
      </Card>

      <Card pad={false}>
        <CardHead
          title="Últimos movimientos"
          sub="Toda variación de stock pasa por aquí"
          right={<Link to="/movimientos" style={{ fontSize: 12 }}>Ver historial</Link>}
        />
        <DataTable
          columns={movementColumns}
          rows={recent.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={recent.isLoading}
          rowTone={(row) => (row.reversalOfMovementId ? 'var(--surf2)' : undefined)}
          empty={(
            <EmptyState
              title="Sin movimientos registrados"
              detail="El stock de los productos empieza en cero y sólo cambia registrando movimientos."
            />
          )}
        />
      </Card>
    </>
  );
}
