import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { roles as rolesApi, users } from '../api/endpoints';
import { ApiError } from '../api/problem';
import type { RoleResponse, UserResponse } from '../api/types';
import { useAuth, useCurrentUser } from '../auth/useAuth';
import { P } from '../auth/permissions';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input, SearchInput, Select, Toggle } from '../components/Field';
import { ConfirmModal, Modal } from '../components/Modal';
import { useToast } from '../components/useToast';
import { Button, Card, Chip, EmptyState, IconButton, Note } from '../components/ui';
import { dateTime, initials, relative } from '../lib/format';
import { usePageMeta } from '../lib/hooks';
import {
  boolParam, numberParam, pageSizeParam, stringParam, useDebouncedParam, useUrlFilters,
} from '../lib/urlFilters';

const FORM_ID = 'user-form';
const RESET_FORM_ID = 'reset-password-form';

/** Los roles se cargan del catálogo de datos: ya no son un enum. */
function useRoleOptions() {
  return useQuery({
    queryKey: ['roles', 'picker'],
    queryFn: () => rolesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  });
}

interface UserForm {
  name: string; email: string; roleId: string;
  isActive: boolean; password: string; confirmPassword: string;
}

function UserModal({
  editing, onClose,
}: { editing: UserResponse | null; onClose: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const roleList = useRoleOptions();
  const [error, setError] = useState<ApiError | null>(null);

  const [form, setForm] = useState<UserForm>(() => (editing ? {
    name: editing.name, email: editing.email, roleId: editing.role.id,
    isActive: editing.isActive, password: '', confirmPassword: '',
  } : {
    name: '', email: '', roleId: '',
    isActive: true, password: '', confirmPassword: '',
  }));

  const set = <K extends keyof UserForm>(key: K, value: UserForm[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const selectedRole = roleList.data?.items.find((role) => role.id === form.roleId);

  const save = useMutation({
    mutationFn: () => (editing
      ? users.update(editing.id, {
        name: form.name.trim(), email: form.email.trim(),
        roleId: form.roleId, isActive: form.isActive,
      })
      : users.create({
        name: form.name.trim(), email: form.email.trim(),
        password: form.password, confirmPassword: form.confirmPassword,
        roleId: form.roleId,
      })),
    onSuccess: (saved) => {
      toast.success(editing ? 'Usuario actualizado' : 'Usuario creado', saved.email);
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      onClose();
    },
    onError: (caught) => {
      if (caught instanceof ApiError
        && (caught.isValidation || caught.isConflict || caught.isNotFound)) {
        setError(caught);
        if (!caught.isValidation) toast.fromError(caught);
      } else {
        toast.fromError(caught);
      }
    },
  });

  const valid = form.name.trim() && form.email.trim() && form.roleId
    && (editing || (form.password.length >= 8 && form.password === form.confirmPassword));

  return (
    <Modal
      title={editing ? 'Editar usuario' : 'Nuevo usuario'}
      subtitle={editing ? editing.email : 'Se crea con la contraseña que definas'}
      onClose={onClose}
      width={500}
      footer={(
        <>
          <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            {selectedRole
              ? `${selectedRole.permissionCount} permisos con este rol`
              : 'El rol define qué ve en el menú'}
          </span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            <Button onClick={onClose}>Cancelar</Button>
            <Button kind="primary" type="submit" form={FORM_ID} disabled={!valid}
              loading={save.isPending}>
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
        <Field label="Nombre" width="100%" required error={error?.fieldError('name')}>
          <Input value={form.name} onChange={(v) => set('name', v)} autoFocus
            error={Boolean(error?.fieldError('name'))} />
        </Field>

        <Field label="Email" width="100%" required error={error?.fieldError('email')}>
          <Input type="email" value={form.email} onChange={(v) => set('email', v)}
            error={Boolean(error?.fieldError('email'))} />
        </Field>

        <Field
          label="Rol"
          width="100%"
          required
          error={error?.fieldError('roleId')}
          hint={selectedRole?.description ?? 'Los roles se administran en la sección Roles'}
        >
          <Select
            value={form.roleId}
            onChange={(v) => set('roleId', v)}
            placeholder={roleList.isLoading ? 'Cargando roles…' : 'Selecciona un rol'}
            error={Boolean(error?.fieldError('roleId'))}
            options={(roleList.data?.items ?? []).map((role) => ({
              value: role.id,
              label: `${role.name} · ${role.permissionCount} permisos`,
            }))}
          />
        </Field>

        {editing ? (
          <>
            <Toggle
              checked={form.isActive}
              onChange={(v) => set('isActive', v)}
              label={form.isActive ? 'Cuenta activa' : 'Cuenta desactivada'}
            />
            <Note tone="neutral" icon="lock">
              La contraseña no se cambia desde aquí. Cada usuario cambia la suya, o alguien
              con permiso la restablece desde la lista.
            </Note>
          </>
        ) : (
          <>
            <Field
              label="Contraseña"
              width="100%"
              required
              error={error?.fieldError('password')}
              hint="mín. 8 caracteres, con mayúscula, minúscula y número"
            >
              <Input type="password" value={form.password} onChange={(v) => set('password', v)}
                error={Boolean(error?.fieldError('password'))} />
            </Field>
            <Field
              label="Repetir contraseña"
              width="100%"
              required
              error={error?.fieldError('confirmPassword')
                ?? (form.confirmPassword && form.password !== form.confirmPassword
                  ? 'Las contraseñas no coinciden.'
                  : undefined)}
            >
              <Input type="password" value={form.confirmPassword}
                onChange={(v) => set('confirmPassword', v)}
                error={Boolean(form.confirmPassword) && form.password !== form.confirmPassword} />
            </Field>
          </>
        )}
      </form>
    </Modal>
  );
}

/** Restablece la contraseña de OTRO usuario. Exige users.change_password. */
function ResetPasswordModal({
  user, onClose,
}: { user: UserResponse; onClose: () => void }) {
  const toast = useToast();
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [revoke, setRevoke] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  const reset = useMutation({
    mutationFn: () => users.resetPassword(user.id, {
      newPassword: password,
      confirmPassword: confirm,
      revokeSessions: revoke,
    }),
    onSuccess: () => {
      toast.success('Contraseña restablecida',
        revoke ? `${user.name} tendrá que entrar de nuevo` : user.name);
      onClose();
    },
    onError: (caught) => {
      if (caught instanceof ApiError && (caught.isValidation || caught.isConflict)) {
        setError(caught);
        if (caught.isConflict) toast.fromError(caught);
      } else {
        toast.fromError(caught, 'No se pudo restablecer');
      }
    },
  });

  const mismatch = Boolean(confirm) && password !== confirm;
  const valid = password.length >= 8 && !mismatch;

  return (
    <Modal
      title="Restablecer contraseña"
      subtitle={`${user.name} · ${user.email}`}
      onClose={onClose}
      width={480}
      footer={(
        <>
          <span
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6,
              fontSize: 11.5, color: 'var(--ink2)',
            }}
          >
            <code style={{ fontFamily: 'var(--mono)', fontSize: 11 }}>
              users.change_password
            </code>
          </span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            <Button onClick={onClose}>Cancelar</Button>
            <Button kind="primary" type="submit" form={RESET_FORM_ID} disabled={!valid}
              loading={reset.isPending}>
              Restablecer
            </Button>
          </span>
        </>
      )}
    >
      <form
        id={RESET_FORM_ID}
        onSubmit={(event) => { event.preventDefault(); setError(null); reset.mutate(); }}
        style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
      >
        <Note tone="warn" icon="lock">
          Estás cambiando la contraseña de <strong>otra persona</strong>. No necesitas la
          contraseña actual, pero la acción queda registrada a tu nombre.
        </Note>

        <Field
          label="Contraseña nueva"
          width="100%"
          required
          error={error?.fieldError('newPassword')}
          hint="mín. 8 caracteres, con mayúscula, minúscula y número"
        >
          <Input type="password" value={password} onChange={setPassword} autoFocus
            error={Boolean(error?.fieldError('newPassword'))} />
        </Field>

        <Field
          label="Repetir contraseña"
          width="100%"
          required
          error={error?.fieldError('confirmPassword')
            ?? (mismatch ? 'Las contraseñas no coinciden.' : undefined)}
        >
          <Input type="password" value={confirm} onChange={setConfirm} error={mismatch} />
        </Field>

        <Toggle
          checked={revoke}
          onChange={setRevoke}
          label={revoke ? 'Cerrar sus sesiones activas' : 'Mantener sus sesiones abiertas'}
        />
        <span style={{ fontSize: 11, color: 'var(--ink3)', lineHeight: 1.5, marginTop: -8 }}>
          {revoke
            ? 'Revoca sus tokens de refresco, así que tendrá que entrar con la contraseña nueva.'
            : 'Si su sesión sigue abierta, seguirá dentro sin usar la contraseña nueva.'}
        </span>
      </form>
    </Modal>
  );
}

export function Users() {
  const me = useCurrentUser();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { can } = useAuth();

  const canCreate = can(P.users.create);
  const canEdit = can(P.users.edit);
  const canDelete = can(P.users.delete);
  const canResetPassword = can(P.users.changePassword);

  const filters = useUrlFilters({
    page: numberParam(1, { min: 1, pagination: true }),
    pageSize: pageSizeParam(),
    search: stringParam(),
    roleId: stringParam(),
    onlyInactive: boolParam(),
  });
  const { page, pageSize, search, roleId, onlyInactive } = filters.values;

  const [searchInput, setSearchInput] = useDebouncedParam(
    search, (value) => filters.set('search', value));

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<UserResponse | null>(null);
  const [removing, setRemoving] = useState<UserResponse | null>(null);
  const [resetting, setResetting] = useState<UserResponse | null>(null);

  // Los tres filtros se resuelven en SQL (regla 3).
  const query = {
    page,
    pageSize,
    search: search || undefined,
    roleId: roleId || undefined,
    isActive: onlyInactive ? false : undefined,
  };
  const list = useQuery({
    queryKey: ['users', query],
    queryFn: () => users.list(query),
  });
  const roleList = useRoleOptions();

  const remove = useMutation({
    mutationFn: (id: string) => users.remove(id),
    onSuccess: () => {
      toast.success('Usuario eliminado');
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      setRemoving(null);
    },
    onError: (caught) => {
      toast.fromError(caught, 'No se pudo eliminar');
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      setRemoving(null);
    },
  });

  usePageMeta({
    title: 'Usuarios',
    subtitle: list.data ? `${list.data.totalCount} usuarios` : undefined,
    actions: canCreate
      ? <Button kind="primary" icon="plus" onClick={() => setCreating(true)}>Nuevo usuario</Button>
      : undefined,
  }, [list.data?.totalCount, canCreate]);

  const roleById = new Map<string, RoleResponse>(
    (roleList.data?.items ?? []).map((role) => [role.id, role]),
  );

  const columns: Column<UserResponse>[] = [
    {
      key: 'name', header: 'Nombre',
      render: (row) => (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 9 }}>
          <span
            aria-hidden
            style={{
              width: 26, height: 26, borderRadius: 6, flexShrink: 0,
              background: 'var(--surf3)', border: '1px solid var(--bord)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 10.5, fontWeight: 600, color: 'var(--ink2)',
            }}
          >
            {initials(row.name)}
          </span>
          <span style={{ fontWeight: 500 }}>{row.name}</span>
          {row.id === me.id ? <Chip tone="acc">tú</Chip> : null}
        </span>
      ),
    },
    {
      key: 'email', header: 'Email', width: 210,
      render: (row) => <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.email}</span>,
    },
    {
      key: 'role', header: 'Rol', width: 160,
      render: (row) => {
        const detail = roleById.get(row.role.id);
        return (
          <span
            title={detail ? `${detail.permissionCount} permisos` : undefined}
            style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}
          >
            <Chip tone={row.role.isSystem ? 'acc' : 'adj'} icon={row.role.isSystem ? 'shield' : undefined}>
              {row.role.name}
            </Chip>
            {detail ? (
              <span className="num" style={{ fontSize: 10.5, color: 'var(--ink3)' }}>
                {detail.permissionCount}
              </span>
            ) : null}
          </span>
        );
      },
    },
    {
      key: 'status', header: 'Estado', width: 116,
      render: (row) => (row.isActive
        ? <Chip tone="in">Activo</Chip>
        : <Chip tone="neutral">Desactivado</Chip>),
    },
    {
      key: 'last', header: 'Último ingreso', width: 130,
      render: (row) => (
        <span style={{ fontSize: 12, color: 'var(--ink2)' }} title={dateTime(row.lastLoginAt)}>
          {relative(row.lastLoginAt)}
        </span>
      ),
    },
    ...(canEdit || canDelete || canResetPassword ? [{
      key: 'actions', header: '', align: 'right' as const, width: 92,
      render: (row: UserResponse) => {
        const isMe = row.id === me.id;
        return (
          <span style={{ display: 'inline-flex', gap: 2 }}>
            {canEdit ? <IconButton icon="pencil" title="Editar" onClick={() => setEditing(row)} /> : null}
            {canResetPassword ? (
              <IconButton
                icon="lock"
                title={isMe
                  ? 'Para tu propia cuenta usa el cambio con contraseña actual'
                  : 'Restablecer contraseña'}
                disabled={isMe}
                onClick={() => setResetting(row)}
              />
            ) : null}
            {canDelete ? (
              <IconButton
                icon="trash"
                tone="dang"
                title={isMe ? 'No puedes eliminar tu propia cuenta' : 'Eliminar'}
                disabled={isMe}
                onClick={() => setRemoving(row)}
              />
            ) : null}
          </span>
        );
      },
    }] : []),
  ];

  return (
    <>
      <Note tone="acc">
        El rol se elige del catálogo de <strong>roles configurables</strong>. La interfaz
        esconde lo que tu rol no permite, pero la autorización la impone la API: pedir el
        endpoint a mano responde <strong>403</strong>.
      </Note>

      <Card pad={false}>
        <FilterBar
          right={(
            <SearchInput value={searchInput} onChange={setSearchInput}
              placeholder="Buscar por nombre o email…" width={260} />
          )}
        >
          <Field label="Rol" width={200}>
            <Select
              value={roleId}
              onChange={(v) => filters.set('roleId', v)}
              placeholder="Todos los roles"
              options={(roleList.data?.items ?? []).map((role) => ({
                value: role.id,
                label: `${role.name} · ${role.userCount}`,
              }))}
            />
          </Field>
          <Field label="Estado" width={168}>
            <Toggle
              checked={onlyInactive}
              onChange={(v) => filters.set('onlyInactive', v)}
              label="Solo desactivados"
            />
          </Field>
          {filters.isFiltered
            ? <Button icon="x" onClick={filters.reset}>Limpiar</Button>
            : null}
        </FilterBar>

        <DataTable
          columns={columns}
          rows={list.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={list.isLoading}
          rowTone={(row) => (row.isActive ? undefined : 'var(--surf2)')}
          empty={(
            <EmptyState
              title={filters.isFiltered ? 'Sin resultados con estos filtros' : 'Sin usuarios'}
              detail={filters.isFiltered
                ? 'Prueba con otro término o limpia los filtros.'
                : undefined}
              action={filters.isFiltered ? (
                <Button icon="x" size="sm" onClick={filters.reset}>Limpiar filtros</Button>
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

      <Note tone="warn" icon="lock">
        No se puede dejar el sistema sin nadie que administre: si el cambio quitaría el
        último usuario activo con <code style={{ fontFamily: 'var(--mono)' }}>roles.edit</code> y{' '}
        <code style={{ fontFamily: 'var(--mono)' }}>users.edit</code>, la API responde{' '}
        <strong>409</strong>. Tampoco se puede eliminar la propia cuenta.
      </Note>

      {creating ? <UserModal editing={null} onClose={() => setCreating(false)} /> : null}
      {editing ? <UserModal editing={editing} onClose={() => setEditing(null)} /> : null}
      {resetting ? (
        <ResetPasswordModal user={resetting} onClose={() => setResetting(null)} />
      ) : null}
      {removing ? (
        <ConfirmModal
          title="Eliminar usuario"
          confirmLabel="Eliminar"
          loading={remove.isPending}
          onClose={() => setRemoving(null)}
          onConfirm={() => remove.mutate(removing.id)}
          message={(
            <>
              Se eliminará <strong>{removing.name}</strong> ({removing.email}).
              <div style={{ marginTop: 8 }}>
                Si el usuario registró movimientos de inventario, la API lo{' '}
                <strong>desactiva</strong> en lugar de borrarlo, para conservar la auditoría.
              </div>
            </>
          )}
        />
      ) : null}
    </>
  );
}
