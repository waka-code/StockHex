import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/problem';
import { roles as rolesApi } from '../api/endpoints';
import type { RoleResponse } from '../api/types';
import { useAuth } from '../auth/useAuth';
import { P } from '../auth/permissions';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input, SearchInput, TextArea } from '../components/Field';
import { Icon } from '../components/Icon';
import { ConfirmModal, Modal } from '../components/Modal';
import { usePermissionCatalog } from '../components/usePermissionCatalog';
import { useToast } from '../components/useToast';
import { Bar, Button, Card, Chip, EmptyState, IconButton, Kpi, Note } from '../components/ui';
import { num } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import {
  numberParam, pageSizeParam, stringParam, useDebouncedParam, useUrlFilters,
} from '../lib/urlFilters';

const FORM_ID = 'role-form';

/**
 * Alta de rol. Sólo pide nombre, descripción y de qué rol partir: los permisos se
 * ajustan después en el editor, que es donde cabe la matriz completa.
 */
function NewRoleModal({
  existing, onClose, onCreated,
}: {
  existing: RoleResponse[];
  onClose: () => void;
  onCreated: (role: RoleResponse) => void;
}) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const catalog = usePermissionCatalog();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [copyFrom, setCopyFrom] = useState('');
  const [error, setError] = useState<ApiError | null>(null);

  const source = existing.find((role) => role.id === copyFrom);
  const inherited = source?.permissions ?? [];

  const create = useMutation({
    mutationFn: () => rolesApi.create({
      name: name.trim(),
      description: description.trim() || null,
      permissions: inherited,
    }),
    onSuccess: (role) => {
      toast.success('Rol creado', `${role.name} · ${role.permissionCount} permisos`);
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      onCreated(role);
    },
    onError: (caught) => {
      if (caught instanceof ApiError && (caught.isValidation || caught.isConflict)) {
        setError(caught);
        if (caught.isConflict) toast.fromError(caught);
      } else {
        toast.fromError(caught);
      }
    },
  });

  return (
    <Modal
      title="Nuevo rol"
      subtitle="El nombre debe ser único"
      onClose={onClose}
      width={520}
      footer={(
        <>
          <span className="num" style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            {inherited.length} de {catalog.data?.totalCount ?? '—'} permisos heredados
          </span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            <Button onClick={onClose}>Cancelar</Button>
            <Button
              kind="primary"
              type="submit"
              form={FORM_ID}
              disabled={!name.trim()}
              loading={create.isPending}
            >
              Crear y editar permisos
            </Button>
          </span>
        </>
      )}
    >
      <form
        id={FORM_ID}
        onSubmit={(event) => { event.preventDefault(); setError(null); create.mutate(); }}
        style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
      >
        <Field label="Nombre del rol" width="100%" required error={error?.fieldError('name')}>
          <Input value={name} onChange={setName} autoFocus placeholder="Cajero de turno"
            error={Boolean(error?.fieldError('name'))} />
        </Field>

        <Field label="Descripción" width="100%" error={error?.fieldError('description')}>
          <TextArea value={description} onChange={setDescription} rows={2}
            placeholder="Registra salidas del mostrador" />
        </Field>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <span style={{ fontSize: 11, fontWeight: 500, color: 'var(--ink2)' }}>Partir desde</span>
          <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap' }}>
            <button
              type="button"
              onClick={() => setCopyFrom('')}
              style={chipStyle(copyFrom === '')}
            >
              Sin permisos
            </button>
            {existing.map((role) => (
              <button
                key={role.id}
                type="button"
                onClick={() => setCopyFrom(role.id)}
                style={chipStyle(copyFrom === role.id)}
              >
                {role.name}
                <span className="num" style={{ opacity: 0.65, marginLeft: 5 }}>
                  {role.permissionCount}
                </span>
              </button>
            ))}
          </div>
          <span style={{ fontSize: 11, color: 'var(--ink3)' }}>
            Duplicar un rol parecido es más rápido que marcar {catalog.data?.totalCount ?? 'todas las'} casillas.
          </span>
        </div>

        {source ? (
          <Note tone="acc">
            Al guardar se abre el editor con los <strong>{inherited.length}</strong> permisos
            heredados de <strong>{source.name}</strong> para ajustarlos.
          </Note>
        ) : null}
      </form>
    </Modal>
  );
}

function chipStyle(selected: boolean) {
  return {
    padding: '6px 11px', borderRadius: 'var(--r)', fontSize: 12, fontWeight: 500,
    background: selected ? 'var(--acc-soft)' : 'var(--surf)',
    color: selected ? 'var(--acc)' : 'var(--ink2)',
    border: `1px solid ${selected ? 'var(--acc-ring)' : 'var(--bord2)'}`,
    cursor: 'pointer',
  } as const;
}

export function Roles() {
  const toast = useToast();
  const queryClient = useQueryClient();
  const { can } = useAuth();

  const canCreate = can(P.roles.create);
  const canEdit = can(P.roles.edit);
  const canDelete = can(P.roles.delete);

  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
    search: stringParam(),
  });
  const { page, pageSize, search } = filters.values;

  const [searchInput, setSearchInput] = useDebouncedParam(
    search, (value) => filters.set('search', value));

  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<RoleResponse | null>(null);

  const query = { page, pageSize, search: search || undefined };
  const list = useQuery({
    queryKey: ['roles', query],
    queryFn: () => rolesApi.list(query),
  });
  const catalog = usePermissionCatalog();
  const total = catalog.data?.totalCount ?? 0;

  const remove = useMutation({
    mutationFn: (id: string) => rolesApi.remove(id),
    onSuccess: () => {
      toast.success('Rol eliminado');
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      setRemoving(null);
    },
    onError: (caught) => {
      toast.fromError(caught, 'No se pudo eliminar');
      setRemoving(null);
    },
  });

  usePageMeta({
    title: 'Roles y permisos',
    subtitle: list.data ? `${list.data.totalCount} roles` : undefined,
    actions: canCreate
      ? <Button kind="primary" icon="plus" onClick={() => setCreating(true)}>Nuevo rol</Button>
      : undefined,
  }, [list.data?.totalCount, canCreate]);

  const items = list.data?.items ?? [];
  const withoutUsers = items.filter((role) => role.userCount === 0).length;

  const columns: Column<RoleResponse>[] = [
    {
      key: 'name', header: 'Rol',
      render: (row) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <span
            aria-hidden
            style={{
              width: 26, height: 26, borderRadius: 6, flexShrink: 0,
              background: row.isSystem ? 'var(--acc-soft)' : 'var(--surf3)',
              border: `1px solid ${row.isSystem ? 'var(--acc-ring)' : 'var(--bord)'}`,
              color: row.isSystem ? 'var(--acc)' : 'var(--ink2)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
            }}
          >
            <Icon name={row.isSystem ? 'shield' : 'lock'} size={14} />
          </span>
          <span style={{ minWidth: 0 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7, flexWrap: 'wrap' }}>
              {canEdit
                ? <Link to={`/roles/${row.id}`} style={{ fontWeight: 500, color: 'var(--ink)' }}>
                    {row.name}
                  </Link>
                : <span style={{ fontWeight: 500 }}>{row.name}</span>}
              {row.isSystem ? <Chip tone="acc" icon="lock">sistema</Chip> : null}
            </span>
            {row.description ? (
              <div style={{ fontSize: 11, color: 'var(--ink3)', marginTop: 1 }}>
                {row.description}
              </div>
            ) : null}
          </span>
        </div>
      ),
    },
    {
      key: 'permissions', header: 'Permisos', width: 220,
      render: (row) => (
        <span style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <Bar value={row.permissionCount} max={Math.max(1, total)} color="var(--acc)" />
          <span className="num" style={{ fontWeight: 500, whiteSpace: 'nowrap' }}>
            {row.permissionCount}/{total || '—'}
          </span>
        </span>
      ),
    },
    {
      key: 'users', header: 'Usuarios', align: 'right', width: 90,
      render: (row) => (row.userCount > 0
        ? <span className="num" style={{ fontWeight: 500 }}>{row.userCount}</span>
        : <span className="num" style={{ color: 'var(--ink3)' }}>0</span>),
    },
    ...(canEdit || canDelete ? [{
      key: 'actions', header: '', align: 'right' as const, width: 70,
      render: (row: RoleResponse) => (
        <span style={{ display: 'inline-flex', gap: 2 }}>
          {canEdit ? (
            <Link to={`/roles/${row.id}`} aria-label="Editar" title="Editar permisos"
              style={{ display: 'inline-flex' }}>
              <IconButton icon="pencil" title="Editar permisos" />
            </Link>
          ) : null}
          {canDelete ? (
            <IconButton
              icon="trash"
              tone="dang"
              title={row.isSystem
                ? 'Un rol de sistema no se elimina'
                : row.userCount > 0
                  ? 'Tiene usuarios asignados'
                  : 'Eliminar'}
              disabled={row.isSystem || row.userCount > 0}
              onClick={() => setRemoving(row)}
            />
          ) : null}
        </span>
      ),
    }] : []),
  ];

  return (
    <>
      <Note tone="acc">
        Los <strong>roles son datos</strong>: se crean, editan y eliminan. Los{' '}
        <strong>permisos tienen una sola fuente</strong>, el código
        {total ? ` (${total} en ${catalog.data?.modules.length} módulos)` : ''}, porque un
        permiso existe sólo si un endpoint lo comprueba.
      </Note>

      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        <Kpi label="Roles" value={num(list.data?.totalCount ?? 0)} icon="lock"
          foot={`${items.filter((r) => r.isSystem).length} de sistema`} />
        <Kpi label="Permisos" value={num(total)} icon="shield"
          foot={`${catalog.data?.modules.length ?? 0} módulos · fuente única en el código`} />
        <Kpi label="Usuarios asignados" value={num(items.reduce((a, r) => a + r.userCount, 0))}
          icon="users" />
        <Kpi label="Sin usuarios" value={num(withoutUsers)} icon="info"
          tone={withoutUsers > 0 ? 'var(--ink3)' : undefined}
          foot={withoutUsers > 0 ? 'se pueden eliminar' : 'todos en uso'} />
      </div>

      <Card pad={false}>
        <FilterBar>
          <SearchInput value={searchInput} onChange={setSearchInput}
            placeholder="Buscar por nombre o descripción…" width={280} />
        </FilterBar>

        <DataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          loading={list.isLoading}
          empty={(
            <EmptyState
              title={search ? `Sin resultados para «${search}»` : 'Sin roles'}
              detail={search ? 'Prueba con otro término.' : undefined}
              action={search
                ? <Button icon="x" size="sm" onClick={filters.reset}>Limpiar</Button>
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

      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        <Note tone="warn" icon="lock">
          Un rol <strong>de sistema</strong> no se puede eliminar ni dejar sin permisos:
          sin él se perdería el acceso a la propia administración.
        </Note>
        <Note tone="warn" icon="alert">
          Un rol <strong>con usuarios asignados</strong> tampoco se elimina. Primero hay que
          reasignar esos usuarios; la API responde <strong>409</strong>.
        </Note>
      </div>

      {creating ? (
        <NewRoleModal
          existing={items}
          onClose={() => setCreating(false)}
          onCreated={() => setCreating(false)}
        />
      ) : null}

      {removing ? (
        <ConfirmModal
          title="Eliminar rol"
          confirmLabel="Eliminar"
          loading={remove.isPending}
          onClose={() => setRemoving(null)}
          onConfirm={() => remove.mutate(removing.id)}
          message={(
            <>
              Se eliminará el rol <strong>{removing.name}</strong> y sus{' '}
              <span className="num">{removing.permissionCount}</span> permisos.
            </>
          )}
        />
      ) : null}
    </>
  );
}
