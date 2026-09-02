import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { categories, products, suppliers } from '../api/endpoints';
import type {
  CreateProductRequest, ProductResponse, UpdateProductRequest,
} from '../api/types';
import { ApiError } from '../api/problem';
import { useAuth } from '../auth/useAuth';
import { P } from '../auth/permissions';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input, SearchInput, Select, TextArea, Toggle } from '../components/Field';
import { ConfirmModal, Modal } from '../components/Modal';
import { useToast } from '../components/useToast';
import { Button, Card, Chip, EmptyState, IconButton, Note } from '../components/ui';
import { clp } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import {
  boolParam, enumParam, numberParam, pageSizeParam, stringParam, useDebouncedParam, useUrlFilters,
} from '../lib/urlFilters';

const FORM_ID = 'product-form';

interface FormState {
  name: string; description: string; sku: string;
  price: string; minimumStock: string;
  categoryId: string; supplierId: string; isActive: boolean;
}

const EMPTY: FormState = {
  name: '', description: '', sku: '', price: '', minimumStock: '0',
  categoryId: '', supplierId: '', isActive: true,
};

function ProductModal({
  editing, onClose,
}: { editing: ProductResponse | null; onClose: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [error, setError] = useState<ApiError | null>(null);

  const [form, setForm] = useState<FormState>(() => (editing ? {
    name: editing.name,
    description: editing.description ?? '',
    sku: editing.sku,
    price: String(editing.price),
    minimumStock: String(editing.minimumStock),
    categoryId: editing.categoryId,
    supplierId: editing.supplierId ?? '',
    isActive: editing.isActive,
  } : EMPTY));

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const categoryList = useQuery({
    queryKey: ['categories', 'picker'],
    queryFn: () => categories.list({ page: 1, pageSize: 100 }),
  });
  const supplierList = useQuery({
    queryKey: ['suppliers', 'picker'],
    queryFn: () => suppliers.list({ page: 1, pageSize: 100 }),
  });

  const save = useMutation({
    mutationFn: () => {
      const body: CreateProductRequest = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        sku: form.sku.trim(),
        price: Number(form.price),
        minimumStock: Number(form.minimumStock),
        categoryId: form.categoryId,
        supplierId: form.supplierId || null,
      };
      return editing
        ? products.update(editing.id, { ...body, isActive: form.isActive } satisfies UpdateProductRequest)
        : products.create(body);
    },
    onSuccess: (saved) => {
      toast.success(editing ? 'Producto actualizado' : 'Producto creado', `${saved.sku} · ${saved.name}`);
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      void queryClient.invalidateQueries({ queryKey: ['reports'] });
      onClose();
    },
    onError: (caught) => {
      if (caught instanceof ApiError && (caught.isValidation || caught.isConflict || caught.isNotFound)) {
        setError(caught);
        if (!caught.isValidation) toast.fromError(caught);
      } else {
        toast.fromError(caught);
      }
    },
  });

  const valid = form.name.trim() && form.sku.trim() && form.categoryId && Number(form.price) > 0;

  return (
    <Modal
      title={editing ? 'Editar producto' : 'Nuevo producto'}
      subtitle={editing
        ? 'El stock no se edita aquí: cámbialo con un movimiento'
        : 'Se crea con stock en cero'}
      onClose={onClose}
      width={560}
      footer={(
        <>
          <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            {editing
              ? `Stock actual ${editing.stockQuantity} · sin cambios`
              : 'Carga las existencias con una entrada'}
          </span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            <Button onClick={onClose}>Cancelar</Button>
            <Button kind="primary" type="submit" form={FORM_ID} disabled={!valid} loading={save.isPending}>
              Guardar
            </Button>
          </span>
        </>
      )}
    >
      <form
        id={FORM_ID}
        onSubmit={(event) => { event.preventDefault(); setError(null); save.mutate(); }}
        style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
      >
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <Field label="Nombre" required error={error?.fieldError('name')}>
            <Input value={form.name} onChange={(v) => set('name', v)} autoFocus
              error={Boolean(error?.fieldError('name'))} />
          </Field>
          <Field label="SKU" width={170} required error={error?.fieldError('sku')}
            hint="letras, números, . - _">
            <Input value={form.sku} onChange={(v) => set('sku', v)}
              error={Boolean(error?.fieldError('sku'))} />
          </Field>
        </div>

        <Field label="Descripción" width="100%" error={error?.fieldError('description')}>
          <TextArea value={form.description} onChange={(v) => set('description', v)} rows={2} />
        </Field>

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <Field label="Precio" width={150} required error={error?.fieldError('price')}
            hint={form.price ? clp(Number(form.price)) : 'en pesos'}>
            <Input type="number" min={0} value={form.price} onChange={(v) => set('price', v)}
              error={Boolean(error?.fieldError('price'))} />
          </Field>
          <Field label="Stock mínimo" width={150} error={error?.fieldError('minimumStock')}
            hint="umbral de alerta">
            <Input type="number" min={0} value={form.minimumStock}
              onChange={(v) => set('minimumStock', v)} />
          </Field>
        </div>

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <Field label="Categoría" required error={error?.fieldError('categoryId')}>
            <Select
              value={form.categoryId}
              onChange={(v) => set('categoryId', v)}
              placeholder="Selecciona…"
              error={Boolean(error?.fieldError('categoryId'))}
              options={(categoryList.data?.items ?? []).map((c) => ({ value: c.id, label: c.name }))}
            />
          </Field>
          <Field label="Proveedor" hint="opcional" error={error?.fieldError('supplierId')}>
            <Select
              value={form.supplierId}
              onChange={(v) => set('supplierId', v)}
              placeholder="Sin proveedor"
              options={(supplierList.data?.items ?? []).map((s) => ({ value: s.id, label: s.name }))}
            />
          </Field>
        </div>

        {editing ? (
          <Toggle
            checked={form.isActive}
            onChange={(v) => set('isActive', v)}
            label={form.isActive ? 'Producto activo' : 'Producto inactivo'}
          />
        ) : null}
      </form>
    </Modal>
  );
}

export function Products() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const { can } = useAuth();

  // Un permiso por acción: un rol puede crear productos y no poder borrarlos.
  const canCreate = can(P.products.create);
  const canEdit = can(P.products.edit);
  const canDelete = can(P.products.delete);
  const writable = canEdit || canDelete;

  // Los filtros viven en la URL (regla 4): refrescar los conserva y el enlace
  // reconstruye la pantalla para quien tenga permiso.
  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
    search: stringParam(),
    categoryId: stringParam(),
    supplierId: stringParam(),
    status: enumParam(['active', 'inactive', 'all'] as const, 'active'),
    lowStockOnly: boolParam(),
  });
  const { page, pageSize, search, categoryId, supplierId, status, lowStockOnly } = filters.values;

  // El texto que se escribe es estado de la UI; se confirma a la URL con retardo
  // para no llenar el historial ni consultar en cada tecla.
  const [searchInput, setSearchInput] = useDebouncedParam(
    search, (value) => filters.set('search', value));

  const [editing, setEditing] = useState<ProductResponse | null>(null);
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<ProductResponse | null>(null);

  const query = useMemo(() => ({
    page, pageSize,
    search: search || undefined,
    categoryId: categoryId || undefined,
    supplierId: supplierId || undefined,
    isActive: status === 'all' ? undefined : status === 'active',
    lowStockOnly: lowStockOnly || undefined,
  }), [page, pageSize, search, categoryId, supplierId, status, lowStockOnly]);

  const list = useQuery({
    queryKey: ['products', query],
    queryFn: () => products.list(query),
  });

  const categoryList = useQuery({
    queryKey: ['categories', 'picker'],
    queryFn: () => categories.list({ page: 1, pageSize: 100 }),
  });
  const supplierList = useQuery({
    queryKey: ['suppliers', 'picker'],
    queryFn: () => suppliers.list({ page: 1, pageSize: 100 }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => products.remove(id),
    onSuccess: () => {
      toast.success('Producto eliminado');
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      void queryClient.invalidateQueries({ queryKey: ['reports'] });
      setRemoving(null);
    },
    onError: (caught) => {
      // La API desactiva en lugar de borrar cuando hay historial y responde 409:
      // hay que refrescar igual porque el estado del producto sí cambió.
      toast.fromError(caught, 'No se pudo eliminar');
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      setRemoving(null);
    },
  });

  usePageMeta({
    title: 'Productos',
    subtitle: list.data
      ? `${list.data.totalCount} ${list.data.totalCount === 1 ? 'producto' : 'productos'}`
      : undefined,
    actions: canCreate
      ? <Button kind="primary" icon="plus" onClick={() => setCreating(true)}>Nuevo producto</Button>
      : undefined,
  }, [list.data?.totalCount, canCreate]);

  const columns: Column<ProductResponse>[] = [
    {
      key: 'sku', header: 'SKU', width: 100,
      render: (row) => <span className="num" style={{ color: 'var(--ink2)' }}>{row.sku}</span>,
    },
    {
      key: 'name', header: 'Nombre',
      render: (row) => (
        <Link to={`/productos/${row.id}`} style={{ fontWeight: 500, color: 'var(--ink)' }}>
          {row.name}
        </Link>
      ),
    },
    {
      key: 'category', header: 'Categoría', width: 118,
      render: (row) => <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.categoryName ?? '—'}</span>,
    },
    {
      key: 'supplier', header: 'Proveedor', width: 152,
      render: (row) => <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.supplierName ?? '—'}</span>,
    },
    {
      key: 'price', header: 'Precio', align: 'right', width: 96,
      render: (row) => <span className="num">{clp(row.price)}</span>,
    },
    {
      key: 'stock', header: 'Stock', align: 'right', width: 112,
      render: (row) => (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, justifyContent: 'flex-end' }}>
          <span
            className="num"
            style={{ color: row.isLowStock ? 'var(--dang)' : 'var(--ink)', fontWeight: 500 }}
          >
            {row.stockQuantity}
          </span>
          {row.isLowStock ? <Chip tone="danger">bajo</Chip> : null}
        </span>
      ),
    },
    {
      key: 'min', header: 'Mín.', align: 'right', width: 62,
      render: (row) => <span className="num" style={{ color: 'var(--ink3)' }}>{row.minimumStock}</span>,
    },
    {
      key: 'status', header: 'Estado', width: 90,
      render: (row) => (row.isActive ? <Chip tone="in">Activo</Chip> : <Chip tone="neutral">Inactivo</Chip>),
    },
    ...(writable ? [{
      key: 'actions', header: '', align: 'right' as const, width: 70,
      render: (row: ProductResponse) => (
        <span style={{ display: 'inline-flex', gap: 2 }}>
          {canEdit
            ? <IconButton icon="pencil" title="Editar" onClick={() => setEditing(row)} />
            : null}
          {canDelete
            ? <IconButton icon="trash" title="Eliminar" tone="dang" onClick={() => setRemoving(row)} />
            : null}
        </span>
      ),
    }] : []),
  ];


  return (
    <>
      <Note tone="acc">
        El stock no se edita desde esta pantalla. Se crea en cero y sólo cambia registrando
        movimientos, para que todo quede auditado.
      </Note>

      <Card pad={false}>
        <FilterBar
          right={<SearchInput value={searchInput} onChange={setSearchInput}
            placeholder="Buscar por nombre o SKU…" width={250} />}
        >
          <Field label="Categoría" width={150}>
            <Select value={categoryId} onChange={(v) => filters.set('categoryId', v)}
              placeholder="Todas"
              options={(categoryList.data?.items ?? []).map((c) => ({ value: c.id, label: c.name }))} />
          </Field>
          <Field label="Proveedor" width={170}>
            <Select value={supplierId} onChange={(v) => filters.set('supplierId', v)}
              placeholder="Todos"
              options={(supplierList.data?.items ?? []).map((s) => ({ value: s.id, label: s.name }))} />
          </Field>
          <Field label="Estado" width={128}>
            <Select
              value={status}
              onChange={(v) => filters.set('status', v === 'inactive' || v === 'all' ? v : 'active')}
              options={[
                { value: 'active', label: 'Activos' },
                { value: 'inactive', label: 'Inactivos' },
                { value: 'all', label: 'Todos' },
              ]}
            />
          </Field>
          <Field label="Stock" width={152}>
            <Toggle checked={lowStockOnly} onChange={(v) => filters.set('lowStockOnly', v)}
              label="Solo stock bajo" tone="danger" />
          </Field>
        </FilterBar>

        <DataTable
          columns={columns}
          rows={list.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={list.isLoading}
          empty={(
            <EmptyState
              title={filters.isFiltered
                ? 'Sin resultados con estos filtros'
                : 'Todavía no hay productos'}
              detail={filters.isFiltered
                ? 'Prueba con otro término o limpia los filtros activos.'
                : 'Crea el primer producto para empezar a registrar movimientos.'}
              action={filters.isFiltered ? (
                <Button icon="x" size="sm" onClick={filters.reset}>Limpiar filtros</Button>
              ) : canCreate ? (
                <Button kind="primary" icon="plus" size="sm" onClick={() => setCreating(true)}>
                  Nuevo producto
                </Button>
              ) : undefined}
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

      {creating ? <ProductModal editing={null} onClose={() => setCreating(false)} /> : null}
      {editing ? <ProductModal editing={editing} onClose={() => setEditing(null)} /> : null}
      {removing ? (
        <ConfirmModal
          title="Eliminar producto"
          confirmLabel="Eliminar"
          loading={remove.isPending}
          onClose={() => setRemoving(null)}
          onConfirm={() => remove.mutate(removing.id)}
          message={(
            <>
              Se eliminará <strong>{removing.sku} · {removing.name}</strong>.
              <div style={{ marginTop: 8 }}>
                Si el producto ya tiene movimientos registrados, la API lo{' '}
                <strong>desactiva</strong> en lugar de borrarlo, para no perder el historial.
              </div>
            </>
          )}
        />
      ) : null}
    </>
  );
}
