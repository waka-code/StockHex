import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { clients, movements, products, suppliers } from '../api/endpoints';
import type { MovementResponse, MovementType } from '../api/types';
import { useAuth, useCurrentUser } from '../auth/useAuth';
import { P } from '../auth/permissions';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input, SearchInput, Select, TextArea, Toggle } from '../components/Field';
import { Icon } from '../components/Icon';
import { Modal } from '../components/Modal';
import { useToast } from '../components/useToast';
import {
  Button, Card, Chip, EmptyState, Kpi, MovementChip, MovementQuantity, Note } from '../components/ui';
import { MOVEMENT } from '../components/tokens';
import { clp, dateTime, dateTimeShort, num } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import {
  boolParam, dateParam, enumParam, guidParam, numberParam, pageSizeParam, stringParam, useDebouncedParam, useUrlFilters,
} from '../lib/urlFilters';
import { NewMovementButton } from './MovementForm';

const REVERSE_FORM = 'reverse-form';

function ReverseModal({
  movement, onClose }: { movement: MovementResponse; onClose: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [comment, setComment] = useState('');

  const delta = movement.stockAfter - movement.stockBefore;
  const reverseType: MovementType = delta > 0 ? 'Out' : 'In';

  const reverse = useMutation({
    mutationFn: () => movements.reverse(movement.id, { comment: comment.trim() || null }),
    onSuccess: (created) => {
      toast.success(
        'Movimiento revertido',
        `Se registró ${MOVEMENT[created.movementType].label} de ${created.quantity} · stock ${created.stockAfter}`,
      );
      void queryClient.invalidateQueries({ queryKey: ['movements'] });
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      void queryClient.invalidateQueries({ queryKey: ['reports'] });
      onClose();
    },
    onError: (caught) => {
      toast.fromError(caught, 'No se pudo revertir');
      onClose();
    } });

  return (
    <Modal
      title="Revertir movimiento"
      subtitle="No se edita ni se borra el original"
      onClose={onClose}
      width={480}
      footer={(
        <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Button onClick={onClose}>Cancelar</Button>
          <Button kind="primary" type="submit" form={REVERSE_FORM} loading={reverse.isPending}>
            Revertir
          </Button>
        </span>
      )}
    >
      <form
        id={REVERSE_FORM}
        onSubmit={(event) => { event.preventDefault(); reverse.mutate(); }}
        style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
      >
        <div
          style={{
            display: 'flex', flexDirection: 'column', gap: 8, padding: '12px 13px',
            background: 'var(--surf3)', border: '1px solid var(--bord)', borderRadius: 'var(--r)' }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 9, flexWrap: 'wrap' }}>
            <MovementChip type={movement.movementType} />
            <MovementQuantity type={movement.movementType} quantity={movement.quantity} />
            <span className="num" style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
              {dateTimeShort(movement.movementDate)}
            </span>
          </div>
          <div style={{ fontSize: 12.5, fontWeight: 500 }}>{movement.productName}</div>
          <div className="num" style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            {movement.stockBefore} → {movement.stockAfter}
            {movement.comment ? <span style={{ fontFamily: 'var(--sans)' }}> · {movement.comment}</span> : null}
          </div>
        </div>

        <Note tone="acc" icon="undo">
          Se registrará una <strong>{MOVEMENT[reverseType].label}</strong> de{' '}
          <span className="num">{Math.abs(delta)}</span> unidades para deshacer este movimiento.
          Se invierte su variación neta, así que es exacto aunque haya habido movimientos
          posteriores.
        </Note>

        <Field label="Motivo" width="100%" hint="queda en el comentario de la corrección">
          <TextArea
            value={comment}
            onChange={setComment}
            placeholder="Orden de compra anulada, error de digitación…"
            rows={2}
          />
        </Field>
      </form>
    </Modal>
  );
}

export function Movements() {
  const user = useCurrentUser();
  const { can } = useAuth();
  const canReverse = can(P.movements.reverse);

  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
    search: stringParam(),
    productId: guidParam(),
    type: enumParam(['', 'In', 'Out', 'Adjustment'] as const, ''),
    partyKind: enumParam(['', 'supplier', 'client'] as const, ''),
    partyId: guidParam(),
    from: dateParam(),
    to: dateParam(),
    onlyMine: boolParam(),
  });
  const {
    page, pageSize, search, productId, type, partyKind, partyId, from, to, onlyMine,
  } = filters.values;

  const [searchInput, setSearchInput] = useDebouncedParam(
    search, (value) => filters.set('search', value));

  const [reversing, setReversing] = useState<MovementResponse | null>(null);

  const query = useMemo(() => ({
    page, pageSize,
    search: search || undefined,
    productId: productId || undefined,
    movementType: (type || undefined) as MovementType | undefined,
    supplierId: partyKind === 'supplier' && partyId ? partyId : undefined,
    clientId: partyKind === 'client' && partyId ? partyId : undefined,
    userId: onlyMine ? user.id : undefined,
    // La API compara contra MovementDate en UTC; el inicio y el fin del día
    // local se envían completos para que el rango incluya ambos extremos.
    from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
    to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined }), [page, pageSize, search, productId, type, partyKind, partyId, onlyMine, user.id, from, to]);

  const list = useQuery({
    queryKey: ['movements', query],
    queryFn: () => movements.list(query) });

  const productList = useQuery({
    queryKey: ['products', 'picker'],
    queryFn: () => products.list({ page: 1, pageSize: 100 }) });
  const supplierList = useQuery({
    queryKey: ['suppliers', 'picker'],
    queryFn: () => suppliers.list({ page: 1, pageSize: 100 }),
    enabled: partyKind === 'supplier' });
  const clientList = useQuery({
    queryKey: ['clients', 'picker'],
    queryFn: () => clients.list({ page: 1, pageSize: 100 }),
    enabled: partyKind === 'client' });

  usePageMeta({
    title: 'Movimientos',
    subtitle: list.data
      ? `${num(list.data.totalCount)} ${list.data.totalCount === 1 ? 'movimiento' : 'movimientos'}`
      : undefined,
    actions: <NewMovementButton /> }, [list.data?.totalCount]);

  // El conjunto de la página, no el total: sirve para leer de un vistazo qué
  // trae la vista filtrada que se está mirando.
  const pageTotals = useMemo(() => {
    const items = list.data?.items ?? [];
    const sum = (t: MovementType) => items
      .filter((m) => m.movementType === t)
      .reduce((acc, m) => acc + m.quantity, 0);
    return {
      in: sum('In'),
      out: sum('Out'),
      adjustments: items.filter((m) => m.movementType === 'Adjustment').length,
      reversals: items.filter((m) => m.reversalOfMovementId !== null).length };
  }, [list.data]);

  const columns: Column<MovementResponse>[] = [
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
      ) },
    {
      key: 'qty', header: 'Cant.', align: 'right', width: 64,
      render: (row) => <MovementQuantity type={row.movementType} quantity={row.quantity} /> },
    {
      key: 'stock', header: 'Stock', width: 100,
      render: (row) => (
        <span className="num">
          <span style={{ color: 'var(--ink3)' }}>{row.stockBefore}</span>
          <span style={{ color: 'var(--ink3)' }}> → </span>
          <span style={{ fontWeight: 500 }}>{row.stockAfter}</span>
        </span>
      ) },
    {
      key: 'price', header: 'P. unit.', align: 'right', width: 90,
      render: (row) => (row.unitPrice === null
        ? <span style={{ color: 'var(--ink3)' }}>—</span>
        : <span className="num">{clp(row.unitPrice)}</span>) },
    {
      key: 'party', header: 'Contraparte', width: 150,
      render: (row) => (
        <span style={{ fontSize: 12, color: 'var(--ink2)' }}>
          {row.clientName ?? row.supplierName ?? '—'}
        </span>
      ) },
    {
      key: 'user', header: 'Usuario', width: 126,
      render: (row) => (row.userId === user.id ? (
        <span
          style={{
            display: 'inline-flex', alignItems: 'center', gap: 6,
            fontSize: 12, fontWeight: 500, color: 'var(--acc)' }}
        >
          <span aria-hidden style={{ width: 5, height: 5, borderRadius: '50%', background: 'var(--acc)' }} />
          {row.userName ?? 'yo'}
        </span>
      ) : (
        <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.userName ?? '—'}</span>
      )) },
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
      ) },
    {
      key: 'comment', header: 'Comentario', width: 220,
      render: (row) => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          <span style={{ fontSize: 11.5, color: 'var(--ink2)' }} title={row.comment ?? undefined}>
            {row.comment
              ? row.comment.length > 40 ? `${row.comment.slice(0, 38)}…` : row.comment
              : '—'}
          </span>
          {row.reversalOfMovementId ? <Chip tone="adj" icon="undo">reversión</Chip> : null}
        </div>
      ) },
    ...(canReverse ? [{
      key: 'actions', header: '', width: 96,
      render: (row: MovementResponse) => (row.reversalOfMovementId ? (
        <span style={{ fontSize: 11, color: 'var(--ink3)' }}>—</span>
      ) : (
        <button
          type="button"
          onClick={() => setReversing(row)}
          style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            padding: '4px 8px', fontSize: 11.5, fontWeight: 500,
            background: 'transparent', color: 'var(--ink2)',
            border: '1px solid var(--bord)', borderRadius: 'var(--r-sm)', cursor: 'pointer' }}
        >
          <span style={{ display: 'flex' }}><Icon name="undo" size={13} /></span>
          Revertir
        </button>
      )) }] : []),
  ];


  return (
    <>
      {!canReverse ? (
        <Note tone="warn" icon="lock">
          Puedes <strong>registrar</strong> movimientos pero no revertirlos: corregir el
          historial es de Admin o Manager. Si te equivocas, avisa a un supervisor.
        </Note>
      ) : null}

      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        <Kpi label="Entradas en la página" value={`+${num(pageTotals.in)}`}
          tone="var(--in)" icon="down" foot="unidades" />
        <Kpi label="Salidas en la página" value={`−${num(pageTotals.out)}`}
          tone="var(--out)" icon="right" foot="unidades" />
        <Kpi label="Ajustes" value={num(pageTotals.adjustments)}
          tone="var(--adj)" icon="filter" foot="en la página" />
        <Kpi label="Reversiones" value={num(pageTotals.reversals)} icon="undo" foot="en la página" />
      </div>

      <Card pad={false}>
        <FilterBar
          right={(
            <>
              <SearchInput value={searchInput} onChange={setSearchInput}
                placeholder="Buscar en comentarios…" width={210} />
              {filters.isFiltered ? <Button icon="x" onClick={filters.reset}>Limpiar</Button> : null}
            </>
          )}
        >
          <Field label="Producto" width={180}>
            <Select value={productId} onChange={(v) => filters.set('productId', v)} placeholder="Todos"
              options={(productList.data?.items ?? []).map((p) => ({
                value: p.id, label: `${p.sku} · ${p.name}` }))} />
          </Field>
          <Field label="Tipo" width={126}>
            <Select
              value={type}
              onChange={(v) => filters.set(
                'type', v === 'In' || v === 'Out' || v === 'Adjustment' ? v : '')}
              placeholder="Todos"
              options={[
                { value: 'In', label: 'Entrada' },
                { value: 'Out', label: 'Salida' },
                { value: 'Adjustment', label: 'Ajuste' },
              ]}
            />
          </Field>
          <Field label="Contraparte" width={140}>
            <Select
              value={partyKind}
              onChange={(value) => filters.setMany({
                partyKind: value === 'supplier' || value === 'client' ? value : '',
                // La contraparte concreta deja de tener sentido al cambiar de tipo.
                partyId: '',
              })}
              placeholder="Cualquiera"
              options={[
                { value: 'supplier', label: 'Proveedor' },
                { value: 'client', label: 'Cliente' },
              ]}
            />
          </Field>
          {partyKind ? (
            <Field label={partyKind === 'supplier' ? '¿Cuál?' : '¿Cuál?'} width={160}>
              <Select value={partyId} onChange={(v) => filters.set('partyId', v)} placeholder="Todos"
                options={((partyKind === 'supplier' ? supplierList.data : clientList.data)?.items ?? [])
                  .map((item) => ({ value: item.id, label: item.name }))} />
            </Field>
          ) : null}
          <Field label="Desde" width={140}>
            <Input type="date" value={from} onChange={(v) => filters.set('from', v)} />
          </Field>
          <Field label="Hasta" width={140}>
            <Input type="date" value={to} onChange={(v) => filters.set('to', v)} />
          </Field>
          <Field label="Usuario" width={148}>
            <Toggle checked={onlyMine} onChange={(v) => filters.set('onlyMine', v)}
              label="Solo los míos" />
          </Field>
        </FilterBar>

        <DataTable
          columns={columns}
          rows={list.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={list.isLoading}
          rowTone={(row) => (row.reversalOfMovementId ? 'var(--surf2)' : undefined)}
          empty={(
            <EmptyState
              title={filters.isFiltered
                ? 'Sin movimientos con estos filtros'
                : 'Todavía no hay movimientos'}
              detail={filters.isFiltered
                ? 'Prueba con otro rango de fechas o limpia los filtros.'
                : 'El stock de los productos empieza en cero y sólo cambia registrando movimientos.'}
              action={filters.isFiltered
                ? <Button icon="x" size="sm" onClick={filters.reset}>Limpiar filtros</Button>
                : undefined}
            />
          )}
        />

        {list.data ? (
          <Pager
            data={list.data}
            onPage={(p) => filters.set('page', p)}
            pageSize={pageSize}
            onPageSize={(size) => filters.set('pageSize', size)}
          />
        ) : null}
      </Card>

      {reversing ? <ReverseModal movement={reversing} onClose={() => setReversing(null)} /> : null}
    </>
  );
}
