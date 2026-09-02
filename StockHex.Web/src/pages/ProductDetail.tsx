import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { movements, products } from '../api/endpoints';
import { ApiError } from '../api/problem';
import type { MovementResponse } from '../api/types';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Icon } from '../components/Icon';
import {
  Card, CardHead, Chip, EmptyState, MovementChip, MovementQuantity, Note, Spinner,
} from '../components/ui';
import { clp, dateTime, dateTimeShort } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import { numberParam, pageSizeParam, useUrlFilters } from '../lib/urlFilters';
import { NewMovementButton } from './MovementForm';

function Definition({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 3, minWidth: 0 }}>
      <span
        style={{
          fontSize: 10.5, fontWeight: 600, color: 'var(--ink2)',
          textTransform: 'uppercase', letterSpacing: '.06em',
        }}
      >
        {label}
      </span>
      <span style={{ fontSize: 13, fontWeight: 500 }}>{value}</span>
    </div>
  );
}

export function ProductDetail() {
  const { id = '' } = useParams();

  // La página del historial también va en la URL: un enlace a la página 3 del
  // historial de un producto tiene que reconstruirse igual.
  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
  });
  const { page, pageSize } = filters.values;

  const product = useQuery({
    queryKey: ['products', id],
    queryFn: () => products.get(id),
    retry: (count, error) => !(error instanceof ApiError && error.isNotFound) && count < 2,
  });

  const history = useQuery({
    queryKey: ['movements', { productId: id, page, pageSize }],
    queryFn: () => movements.list({ productId: id, page, pageSize }),
    enabled: Boolean(product.data),
  });

  usePageMeta({
    title: product.data?.name ?? 'Producto',
    subtitle: product.data ? `Detalle · ${product.data.sku}` : undefined,
    actions: product.data ? <NewMovementButton product={product.data} /> : undefined,
  }, [product.data?.id, product.data?.stockQuantity]);

  if (product.isLoading) return <Card><Spinner label="Cargando producto…" /></Card>;

  if (product.error instanceof ApiError && product.error.isNotFound) {
    return (
      <Card>
        <EmptyState
          icon="alert"
          title="Producto no encontrado"
          detail="El producto no existe o fue eliminado."
          action={<Link to="/productos" style={{ fontSize: 12.5 }}>Volver a Productos</Link>}
        />
      </Card>
    );
  }

  const p = product.data;
  if (!p) return null;

  const columns: Column<MovementResponse>[] = [
    { key: 'type', header: 'Tipo', width: 96, render: (row) => <MovementChip type={row.movementType} /> },
    {
      key: 'qty', header: 'Cant.', align: 'right', width: 64,
      render: (row) => <MovementQuantity type={row.movementType} quantity={row.quantity} />,
    },
    {
      key: 'stock', header: 'Stock', width: 100,
      render: (row) => (
        <span className="num">
          <span style={{ color: 'var(--ink3)' }}>{row.stockBefore}</span>
          <span style={{ color: 'var(--ink3)' }}> → </span>
          <span style={{ fontWeight: 500 }}>{row.stockAfter}</span>
        </span>
      ),
    },
    {
      key: 'price', header: 'P. unit.', align: 'right', width: 92,
      render: (row) => (row.unitPrice === null
        ? <span style={{ color: 'var(--ink3)' }}>—</span>
        : <span className="num">{clp(row.unitPrice)}</span>),
    },
    {
      key: 'party', header: 'Contraparte', width: 152,
      render: (row) => (
        <span style={{ fontSize: 12, color: 'var(--ink2)' }}>
          {row.clientName ?? row.supplierName ?? '—'}
        </span>
      ),
    },
    {
      key: 'user', header: 'Usuario', width: 120,
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
    {
      key: 'comment', header: 'Comentario',
      render: (row) => (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 11.5, color: 'var(--ink2)' }}>{row.comment ?? '—'}</span>
          {row.reversalOfMovementId ? <Chip tone="adj" icon="undo">reversión</Chip> : null}
        </span>
      ),
    },
  ];

  return (
    <>
      <Link
        to="/productos"
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          fontSize: 12.5, color: 'var(--ink2)', alignSelf: 'flex-start',
        }}
      >
        <Icon name="left" size={14} />
        Volver a Productos
      </Link>

      <Card>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 20, flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: 240 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 9, flexWrap: 'wrap' }}>
              <span
                className="num"
                style={{
                  fontSize: 12, color: 'var(--ink2)', background: 'var(--surf3)',
                  border: '1px solid var(--bord)', padding: '2px 7px', borderRadius: 'var(--r-sm)',
                }}
              >
                {p.sku}
              </span>
              {p.isActive ? <Chip tone="in">Activo</Chip> : <Chip tone="neutral">Inactivo</Chip>}
              {p.isLowStock ? <Chip tone="danger" icon="alert">stock bajo</Chip> : null}
            </div>
            <h2 style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.03em', margin: '9px 0 0' }}>
              {p.name}
            </h2>
            {p.description ? (
              <p style={{ fontSize: 12.5, color: 'var(--ink2)', margin: '4px 0 0' }}>{p.description}</p>
            ) : null}
          </div>
        </div>

        <div style={{ height: 1, background: 'var(--bord)', margin: '18px 0' }} />

        <div
          style={{
            display: 'grid', gap: 20,
            gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))',
          }}
        >
          <Definition label="Categoría" value={p.categoryName ?? '—'} />
          <Definition label="Proveedor" value={p.supplierName ?? '—'} />
          <Definition label="Precio" value={<span className="num">{clp(p.price)}</span>} />
          <Definition
            label="Stock actual"
            value={(
              <span className="num" style={{ color: p.isLowStock ? 'var(--dang)' : undefined }}>
                {p.stockQuantity}
              </span>
            )}
          />
          <Definition label="Stock mínimo" value={<span className="num">{p.minimumStock}</span>} />
          <Definition
            label="Valorización"
            value={<span className="num">{clp(p.price * p.stockQuantity)}</span>}
          />
        </div>
      </Card>

      {p.isLowStock ? (
        <Note tone="danger" icon="alert">
          El stock (<span className="num">{p.stockQuantity}</span>) está en o por debajo del mínimo
          (<span className="num">{p.minimumStock}</span>).
          {p.minimumStock - p.stockQuantity > 0 ? (
            <> Faltan <span className="num">{p.minimumStock - p.stockQuantity}</span> unidades para
            salir del reporte de stock bajo.</>
          ) : null}
        </Note>
      ) : null}

      {!p.isActive ? (
        <Note tone="warn" icon="lock">
          El producto está desactivado y no admite movimientos nuevos. Actívalo desde la
          pantalla de Productos si necesitas volver a moverlo.
        </Note>
      ) : null}

      <Card pad={false}>
        <CardHead
          title="Historial de movimientos"
          sub="El stock actual es el acumulado de estos movimientos"
          right={(
            <span style={{ fontSize: 11.5, color: 'var(--ink2)' }}>
              Stock actual{' '}
              <span
                className="num"
                style={{ color: p.isLowStock ? 'var(--dang)' : 'var(--ink)', fontWeight: 500 }}
              >
                {p.stockQuantity}
              </span>
            </span>
          )}
        />
        <DataTable
          columns={columns}
          rows={history.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={history.isLoading}
          rowTone={(row) => (row.reversalOfMovementId ? 'var(--surf2)' : undefined)}
          empty={(
            <EmptyState
              title="Sin movimientos"
              detail="Este producto nunca ha tenido entradas ni salidas, por eso su stock es cero."
            />
          )}
        />
        {history.data ? (
          <Pager
            data={history.data}
            onPage={(p) => filters.set('page', p)}
            pageSize={pageSize}
            onPageSize={(size) => filters.set('pageSize', size)}
          />
        ) : null}
      </Card>
    </>
  );
}
