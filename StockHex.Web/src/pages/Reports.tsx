import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { reports } from '../api/endpoints';
import type { LowStockItemResponse, MovementSummaryLine } from '../api/types';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input } from '../components/Field';
import {
  Bar, Button, Card, CardHead, Chip, EmptyState, Kpi, MovementChip, Spinner } from '../components/ui';
import { MOVEMENT } from '../components/tokens';
import { clp, dateOnly, num, toDateInput } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import { dateParam, numberParam, pageSizeParam, useUrlFilters } from '../lib/urlFilters';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: toDateInput(from), to: toDateInput(to) };
}

export function Reports() {
  // El rango por defecto son los últimos 30 días, y se calcula una sola vez para
  // que sea el valor por omisión del parámetro y no ensucie la URL.
  const initial = useMemo(() => defaultRange(), []);

  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
    from: dateParam(initial.from),
    to: dateParam(initial.to),
  });
  const { page, pageSize, from, to } = filters.values;

  const summary = useQuery({
    queryKey: ['reports', 'inventory-summary'],
    queryFn: () => reports.inventorySummary() });

  const lowStock = useQuery({
    queryKey: ['reports', 'low-stock', { page, pageSize }],
    queryFn: () => reports.lowStock({ page, pageSize }) });

  const movementSummary = useQuery({
    queryKey: ['reports', 'movement-summary', from, to],
    queryFn: () => reports.movementSummary(
      from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
    ) });

  usePageMeta({
    title: 'Reportes',
    subtitle: `Del ${dateOnly(from)} al ${dateOnly(to)}` }, [from, to]);

  const s = summary.data;
  const lines = movementSummary.data?.lines ?? [];
  const totalUnits = lines.reduce((acc, line) => acc + line.units, 0);
  const maxUnits = Math.max(1, ...lines.map((line) => line.units));

  const lowColumns: Column<LowStockItemResponse>[] = [
    {
      key: 'sku', header: 'SKU', width: 96,
      render: (row) => <span className="num" style={{ color: 'var(--ink2)' }}>{row.sku}</span> },
    {
      key: 'name', header: 'Producto',
      render: (row) => (
        <Link to={`/productos/${row.productId}`} style={{ fontWeight: 500, color: 'var(--ink)' }}>
          {row.name}
        </Link>
      ) },
    {
      key: 'category', header: 'Categoría', width: 112,
      render: (row) => (
        <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.categoryName ?? '—'}</span>
      ) },
    {
      key: 'stock', header: 'Stock', align: 'right', width: 66,
      render: (row) => (
        <span className="num" style={{ color: 'var(--dang)', fontWeight: 500 }}>
          {row.stockQuantity}
        </span>
      ) },
    {
      key: 'min', header: 'Mínimo', align: 'right', width: 66,
      render: (row) => <span className="num" style={{ color: 'var(--ink2)' }}>{row.minimumStock}</span> },
    {
      key: 'deficit', header: 'Déficit', width: 168,
      render: (row) => (row.deficit > 0 ? (
        <span style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <Bar value={row.deficit} max={Math.max(1, row.minimumStock)} color="var(--dang)" />
          <span className="num" style={{ color: 'var(--dang)', fontWeight: 500, width: 28, textAlign: 'right' }}>
            {row.deficit}
          </span>
        </span>
      ) : (
        <Chip tone="warn">en el límite</Chip>
      )) },
  ];

  const summaryColumns: Column<MovementSummaryLine>[] = [
    { key: 'type', header: 'Tipo', width: 100, render: (row) => <MovementChip type={row.movementType} /> },
    {
      key: 'movements', header: 'Movs.', align: 'right', width: 70,
      render: (row) => <span className="num" style={{ fontWeight: 500 }}>{num(row.movements)}</span> },
    {
      key: 'units', header: 'Unidades', align: 'right', width: 86,
      render: (row) => (
        <span className="num" style={{ color: MOVEMENT[row.movementType].color, fontWeight: 500 }}>
          {num(row.units)}
        </span>
      ) },
    {
      key: 'share', header: 'Reparto',
      render: (row) => (
        <span style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <Bar value={row.units} max={maxUnits} color={MOVEMENT[row.movementType].color} />
          <span className="num" style={{ fontSize: 11, color: 'var(--ink3)', width: 38, textAlign: 'right' }}>
            {totalUnits > 0 ? `${Math.round((row.units / totalUnits) * 100)}%` : '0%'}
          </span>
        </span>
      ) },
  ];

  return (
    <>
      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        {summary.isLoading || !s ? (
          <Card style={{ flex: 1 }}><Spinner /></Card>
        ) : (
          <>
            <Kpi label="Productos" value={num(s.totalProducts)} icon="box"
              foot={`${num(s.activeProducts)} activos`} />
            <Kpi
              label="En stock bajo"
              value={num(s.lowStockProducts)}
              tone={s.lowStockProducts > 0 ? 'var(--dang)' : undefined}
              icon="alert"
              foot={s.activeProducts > 0
                ? `${Math.round((s.lowStockProducts / s.activeProducts) * 100)}% del catálogo activo`
                : '—'}
            />
            <Kpi label="Valorización" value={clp(s.totalStockValue)} icon="chart"
              foot="precio × stock, activos" />
            <Kpi label="Unidades movidas" value={num(totalUnits)} icon="swap"
              foot="en el período elegido" />
          </>
        )}
      </div>

      <Card pad={false}>
        <CardHead
          title="Movimientos del período"
          sub="Agregado por tipo. Los totales los calcula la base de datos."
        />
        <FilterBar
          right={(
            <Button icon="clock" onClick={filters.reset}>Últimos 30 días</Button>
          )}
        >
          <Field label="Desde" width={160}>
            <Input type="date" value={from} onChange={(v) => filters.set('from', v)} />
          </Field>
          <Field label="Hasta" width={160}>
            <Input type="date" value={to} onChange={(v) => filters.set('to', v)} />
          </Field>
        </FilterBar>
        <DataTable
          columns={summaryColumns}
          rows={lines}
          rowKey={(row) => row.movementType}
          loading={movementSummary.isLoading}
          empty={(
            <EmptyState
              title="Sin movimientos en el período"
              detail="Prueba con un rango de fechas más amplio."
            />
          )}
        />
      </Card>

      <Card pad={false}>
        <CardHead
          title="Stock bajo"
          sub="Paginado y ordenado por déficit en la base de datos"
          right={lowStock.data ? (
            <span style={{ fontSize: 11.5, color: 'var(--ink2)' }}>
              <span className="num" style={{ fontWeight: 500, color: 'var(--dang)' }}>
                {lowStock.data.totalCount}
              </span>{' '}
              {lowStock.data.totalCount === 1 ? 'producto' : 'productos'}
            </span>
          ) : undefined}
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
        {lowStock.data ? (
          <Pager
            data={lowStock.data}
            onPage={(p) => filters.set('page', p)}
            pageSize={pageSize}
            onPageSize={(size) => filters.set('pageSize', size)}
          />
        ) : null}
      </Card>
    </>
  );
}
