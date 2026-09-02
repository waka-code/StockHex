import { categories, clients, suppliers } from '../api/endpoints';
import type { CategoryResponse, ClientResponse, SupplierResponse } from '../api/types';
import { Field, Input, TextArea } from '../components/Field';
import { IconButton, Note } from '../components/ui';
import { P } from '../auth/permissions';
import { CrudPage } from './CrudPage';

/** Papelera deshabilitada cuando hay filas dependientes: la API responde 409. */
function RowActions({
  writable, canEdit, canDelete, blocked, onEdit, onRemove,
}: {
  writable: boolean;
  canEdit: boolean;
  canDelete: boolean;
  blocked: boolean;
  onEdit: () => void;
  onRemove: () => void;
}) {
  if (!writable) return null;
  return (
    <span style={{ display: 'inline-flex', gap: 2 }}>
      {canEdit ? <IconButton icon="pencil" title="Editar" onClick={onEdit} /> : null}
      {canDelete ? (
        <IconButton
          icon="trash"
          title={blocked ? 'Tiene registros asociados' : 'Eliminar'}
          tone="dang"
          disabled={blocked}
          onClick={onRemove}
        />
      ) : null}
    </span>
  );
}

// ─────────────────────────────────────────────────────── Categorías

interface CategoryForm { name: string; description: string; }

export function Categories() {
  return (
    <CrudPage<CategoryResponse, CategoryForm>
      title="Categorías"
      singular="categoría"
      permissions={P.categories}
      gender="f"
      queryKey="categories"
      searchPlaceholder="Buscar por nombre o descripción…"
      list={(query) => categories.list(query)}
      create={(form) => categories.create({
        name: form.name.trim(),
        description: form.description.trim() || null,
      })}
      update={(id, form) => categories.update(id, {
        name: form.name.trim(),
        description: form.description.trim() || null,
      })}
      remove={(id) => categories.remove(id)}
      rowKey={(item) => item.id}
      itemLabel={(item) => item.name}
      blockingCount={(item) => item.productCount}
      blockingLabel="producto"
      emptyForm={{ name: '', description: '' }}
      toForm={(item) => ({ name: item.name, description: item.description ?? '' })}
      isValid={(form) => form.name.trim().length > 0}
      note={(
        <Note tone="acc">
          Una categoría con productos asociados no se puede eliminar: la papelera queda
          deshabilitada y la API responde <strong>409</strong>.
        </Note>
      )}
      columns={({ writable, canEdit, canDelete, edit, remove }) => [
        {
          key: 'name', header: 'Nombre',
          render: (row) => <span style={{ fontWeight: 500 }}>{row.name}</span>,
        },
        {
          key: 'description', header: 'Descripción',
          render: (row) => (
            <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.description ?? '—'}</span>
          ),
        },
        {
          key: 'products', header: 'Productos', align: 'right', width: 92,
          render: (row) => <span className="num" style={{ fontWeight: 500 }}>{row.productCount}</span>,
        },
        {
          key: 'actions', header: '', align: 'right', width: 70,
          render: (row) => (
            <RowActions
              writable={writable}
              canEdit={canEdit}
              canDelete={canDelete}
              blocked={row.productCount > 0}
              onEdit={() => edit(row)}
              onRemove={() => remove(row)}
            />
          ),
        },
      ]}
      renderForm={(form, set, error) => (
        <>
          <Field label="Nombre" width="100%" required error={error?.fieldError('name')}
            hint="debe ser único">
            <Input value={form.name} onChange={(v) => set('name', v)} autoFocus
              error={Boolean(error?.fieldError('name'))} />
          </Field>
          <Field label="Descripción" width="100%" error={error?.fieldError('description')}>
            <TextArea value={form.description} onChange={(v) => set('description', v)} rows={3} />
          </Field>
        </>
      )}
    />
  );
}

// ────────────────────────────────────────────────────── Proveedores

interface SupplierForm { name: string; description: string; phoneNumber: string; email: string; }

export function Suppliers() {
  return (
    <CrudPage<SupplierResponse, SupplierForm>
      title="Proveedores"
      singular="proveedor"
      permissions={P.suppliers}
      gender="m"
      queryKey="suppliers"
      searchPlaceholder="Buscar por nombre o email…"
      list={(query) => suppliers.list(query)}
      create={(form) => suppliers.create({
        name: form.name.trim(),
        description: form.description.trim() || null,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
      })}
      update={(id, form) => suppliers.update(id, {
        name: form.name.trim(),
        description: form.description.trim() || null,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
      })}
      remove={(id) => suppliers.remove(id)}
      rowKey={(item) => item.id}
      itemLabel={(item) => item.name}
      blockingCount={(item) => item.productCount}
      blockingLabel="producto"
      emptyForm={{ name: '', description: '', phoneNumber: '', email: '' }}
      toForm={(item) => ({
        name: item.name,
        description: item.description ?? '',
        phoneNumber: item.phoneNumber ?? '',
        email: item.email ?? '',
      })}
      isValid={(form) => form.name.trim().length > 0}
      note={(
        <Note tone="acc">
          Los proveedores son la contraparte de las entradas. Uno con productos asociados
          no se puede eliminar; la API responde <strong>409</strong>.
        </Note>
      )}
      columns={({ writable, canEdit, canDelete, edit, remove }) => [
        {
          key: 'name', header: 'Nombre',
          render: (row) => (
            <>
              <span style={{ fontWeight: 500 }}>{row.name}</span>
              {row.description ? (
                <div style={{ fontSize: 11, color: 'var(--ink3)', marginTop: 1 }}>{row.description}</div>
              ) : null}
            </>
          ),
        },
        {
          key: 'phone', header: 'Teléfono', width: 148,
          render: (row) => (
            <span className="num" style={{ color: 'var(--ink2)' }}>{row.phoneNumber ?? '—'}</span>
          ),
        },
        {
          key: 'email', header: 'Email', width: 200,
          render: (row) => (row.email
            ? <a href={`mailto:${row.email}`} style={{ fontSize: 12 }}>{row.email}</a>
            : <span style={{ color: 'var(--ink3)' }}>—</span>),
        },
        {
          key: 'products', header: 'Productos', align: 'right', width: 92,
          render: (row) => <span className="num" style={{ fontWeight: 500 }}>{row.productCount}</span>,
        },
        {
          key: 'actions', header: '', align: 'right', width: 70,
          render: (row) => (
            <RowActions
              writable={writable}
              canEdit={canEdit}
              canDelete={canDelete}
              blocked={row.productCount > 0}
              onEdit={() => edit(row)}
              onRemove={() => remove(row)}
            />
          ),
        },
      ]}
      renderForm={(form, set, error) => (
        <>
          <Field label="Nombre" width="100%" required error={error?.fieldError('name')}
            hint="debe ser único">
            <Input value={form.name} onChange={(v) => set('name', v)} autoFocus
              error={Boolean(error?.fieldError('name'))} />
          </Field>
          <Field label="Descripción" width="100%" error={error?.fieldError('description')}>
            <TextArea value={form.description} onChange={(v) => set('description', v)} rows={2} />
          </Field>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Field label="Teléfono" error={error?.fieldError('phoneNumber')}>
              <Input value={form.phoneNumber} onChange={(v) => set('phoneNumber', v)}
                placeholder="+56 9 1234 5678" />
            </Field>
            <Field label="Email" error={error?.fieldError('email')}>
              <Input type="email" value={form.email} onChange={(v) => set('email', v)}
                placeholder="ventas@proveedor.cl" error={Boolean(error?.fieldError('email'))} />
            </Field>
          </div>
        </>
      )}
    />
  );
}

// ───────────────────────────────────────────────────────── Clientes

interface ClientForm { name: string; address: string; phoneNumber: string; email: string; }

export function Clients() {
  return (
    <CrudPage<ClientResponse, ClientForm>
      title="Clientes"
      singular="cliente"
      permissions={P.clients}
      gender="m"
      queryKey="clients"
      searchPlaceholder="Buscar por nombre, email o teléfono…"
      list={(query) => clients.list(query)}
      create={(form) => clients.create({
        name: form.name.trim(),
        address: form.address.trim() || null,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
      })}
      update={(id, form) => clients.update(id, {
        name: form.name.trim(),
        address: form.address.trim() || null,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
      })}
      remove={(id) => clients.remove(id)}
      rowKey={(item) => item.id}
      itemLabel={(item) => item.name}
      emptyForm={{ name: '', address: '', phoneNumber: '', email: '' }}
      toForm={(item) => ({
        name: item.name,
        address: item.address ?? '',
        phoneNumber: item.phoneNumber ?? '',
        email: item.email ?? '',
      })}
      isValid={(form) => form.name.trim().length > 0}
      note={(
        <Note tone="acc">
          Los clientes son la contraparte de las salidas. Uno con movimientos asociados
          no se puede eliminar; la API responde <strong>409</strong>.
        </Note>
      )}
      columns={({ writable, canEdit, canDelete, edit, remove }) => [
        {
          key: 'name', header: 'Nombre',
          render: (row) => <span style={{ fontWeight: 500 }}>{row.name}</span>,
        },
        {
          key: 'address', header: 'Dirección',
          render: (row) => (
            <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.address ?? '—'}</span>
          ),
        },
        {
          key: 'phone', header: 'Teléfono', width: 148,
          render: (row) => (
            <span className="num" style={{ color: 'var(--ink2)' }}>{row.phoneNumber ?? '—'}</span>
          ),
        },
        {
          key: 'email', header: 'Email', width: 200,
          render: (row) => (row.email
            ? <a href={`mailto:${row.email}`} style={{ fontSize: 12 }}>{row.email}</a>
            : <span style={{ color: 'var(--ink3)' }}>—</span>),
        },
        {
          key: 'actions', header: '', align: 'right', width: 70,
          render: (row) => (
            <RowActions
              writable={writable}
              canEdit={canEdit}
              canDelete={canDelete}
              blocked={false}
              onEdit={() => edit(row)}
              onRemove={() => remove(row)}
            />
          ),
        },
      ]}
      renderForm={(form, set, error) => (
        <>
          <Field label="Nombre" width="100%" required error={error?.fieldError('name')}>
            <Input value={form.name} onChange={(v) => set('name', v)} autoFocus
              error={Boolean(error?.fieldError('name'))} />
          </Field>
          <Field label="Dirección" width="100%" error={error?.fieldError('address')}>
            <TextArea value={form.address} onChange={(v) => set('address', v)} rows={2} />
          </Field>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Field label="Teléfono" error={error?.fieldError('phoneNumber')}>
              <Input value={form.phoneNumber} onChange={(v) => set('phoneNumber', v)}
                placeholder="+56 9 1234 5678" />
            </Field>
            <Field label="Email" error={error?.fieldError('email')}
              hint="único, pero opcional">
              <Input type="email" value={form.email} onChange={(v) => set('email', v)}
                placeholder="cliente@correo.cl" error={Boolean(error?.fieldError('email'))} />
            </Field>
          </div>
        </>
      )}
    />
  );
}
