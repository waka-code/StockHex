import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { users } from '../api/endpoints';
import { ApiError } from '../api/problem';
import type { UserResponse, UserRole } from '../api/types';
import { useCurrentUser } from '../auth/useAuth';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { Field, FilterBar, Input, SearchInput, Toggle } from '../components/Field';
import { ConfirmModal, Modal } from '../components/Modal';
import { useToast } from '../components/useToast';
import { Button, Card, Chip, EmptyState, IconButton, Note } from '../components/ui';
import { dateTime, initials, relative } from '../lib/format';
import { useDebounced, usePageMeta, useResetPageOnFilterChange } from '../lib/hooks';

const FORM_ID = 'user-form';
const ROLES: UserRole[] = ['Admin', 'Manager', 'Operator'];

const ROLE_TONE: Record<UserRole, 'acc' | 'adj' | 'neutral'> = {
  Admin: 'acc', Manager: 'adj', Operator: 'neutral',
};

const ROLE_HELP: Record<UserRole, string> = {
  Admin: 'Todo, incluida la gestión de usuarios.',
  Manager: 'Catálogo, contrapartes, movimientos y reportes.',
  Operator: 'Consulta y registro de movimientos.',
};

interface UserForm {
  name: string; email: string; role: UserRole;
  isActive: boolean; password: string; confirmPassword: string;
}

function RolePicker({
  value, onChange, disabled,
}: { value: UserRole; onChange: (role: UserRole) => void; disabled?: boolean }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <span style={{ fontSize: 11, fontWeight: 500, color: 'var(--ink2)' }}>Rol</span>
      <div style={{ display: 'flex', gap: 7 }}>
        {ROLES.map((role) => {
          const selected = value === role;
          return (
            <button
              key={role}
              type="button"
              disabled={disabled}
              onClick={() => onChange(role)}
              aria-pressed={selected}
              style={{
                flex: 1, padding: '7px 0', fontSize: 12, fontWeight: 500,
                borderRadius: 'var(--r)', cursor: disabled ? 'not-allowed' : 'pointer',
                background: selected ? 'var(--acc-soft)' : 'var(--surf)',
                color: selected ? 'var(--acc)' : 'var(--ink2)',
                border: `1px solid ${selected ? 'var(--acc-ring)' : 'var(--bord2)'}`,
                opacity: disabled ? 0.5 : 1,
              }}
            >
              {role}
            </button>
          );
        })}
      </div>
      <span style={{ fontSize: 11, color: 'var(--ink3)' }}>{ROLE_HELP[value]}</span>
    </div>
  );
}

function UserModal({
  editing, onClose,
}: { editing: UserResponse | null; onClose: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [error, setError] = useState<ApiError | null>(null);

  const [form, setForm] = useState<UserForm>(() => (editing ? {
    name: editing.name, email: editing.email, role: editing.role,
    isActive: editing.isActive, password: '', confirmPassword: '',
  } : {
    name: '', email: '', role: 'Operator',
    isActive: true, password: '', confirmPassword: '',
  }));

  const set = <K extends keyof UserForm>(key: K, value: UserForm[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const save = useMutation({
    mutationFn: () => (editing
      ? users.update(editing.id, {
        name: form.name.trim(), email: form.email.trim(),
        role: form.role, isActive: form.isActive,
      })
      : users.create({
        name: form.name.trim(), email: form.email.trim(),
        password: form.password, confirmPassword: form.confirmPassword,
        role: form.role,
      })),
    onSuccess: (saved) => {
      toast.success(editing ? 'Usuario actualizado' : 'Usuario creado', saved.email);
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      onClose();
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

  const valid = form.name.trim() && form.email.trim()
    && (editing || (form.password.length >= 8 && form.password === form.confirmPassword));

  return (
    <Modal
      title={editing ? 'Editar usuario' : 'Nuevo usuario'}
      subtitle={editing ? editing.email : 'Se crea con la contraseña que definas'}
      onClose={onClose}
      width={480}
      footer={(
        <>
          <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            El rol define qué ve en el menú
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

        <RolePicker value={form.role} onChange={(v) => set('role', v)} />

        {editing ? (
          <Toggle
            checked={form.isActive}
            onChange={(v) => set('isActive', v)}
            label={form.isActive ? 'Cuenta activa' : 'Cuenta desactivada'}
          />
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

        {editing ? (
          <Note tone="neutral" icon="lock">
            La contraseña no se cambia desde aquí: cada usuario cambia la suya desde su
            propia sesión.
          </Note>
        ) : null}
      </form>
    </Modal>
  );
}

export function Users() {
  const me = useCurrentUser();
  const toast = useToast();
  const queryClient = useQueryClient();

  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<UserResponse | null>(null);
  const [removing, setRemoving] = useState<UserResponse | null>(null);

  useResetPageOnFilterChange(search, page, setPage);

  const query = { page, pageSize: 20, search: search || undefined };
  const list = useQuery({
    queryKey: ['users', query],
    queryFn: () => users.list(query),
  });

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

  const activeAdmins = (list.data?.items ?? [])
    .filter((user) => user.role === 'Admin' && user.isActive).length;

  usePageMeta({
    title: 'Usuarios',
    subtitle: list.data ? `${list.data.totalCount} usuarios` : undefined,
    actions: <Button kind="primary" icon="plus" onClick={() => setCreating(true)}>Nuevo usuario</Button>,
  }, [list.data?.totalCount]);

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
      key: 'email', header: 'Email', width: 220,
      render: (row) => <span style={{ fontSize: 12, color: 'var(--ink2)' }}>{row.email}</span>,
    },
    {
      key: 'role', header: 'Rol', width: 100,
      render: (row) => <Chip tone={ROLE_TONE[row.role]}>{row.role}</Chip>,
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
    {
      key: 'actions', header: '', align: 'right', width: 70,
      render: (row) => {
        // Se deshabilita lo que la API va a rechazar igualmente, para no
        // ofrecer una acción condenada a un 409.
        const isMe = row.id === me.id;
        const isLastAdmin = row.role === 'Admin' && row.isActive && activeAdmins <= 1;
        return (
          <span style={{ display: 'inline-flex', gap: 2 }}>
            <IconButton icon="pencil" title="Editar" onClick={() => setEditing(row)} />
            <IconButton
              icon="trash"
              tone="dang"
              title={isMe
                ? 'No puedes eliminar tu propia cuenta'
                : isLastAdmin
                  ? 'Es el único administrador activo'
                  : 'Eliminar'}
              disabled={isMe || isLastAdmin}
              onClick={() => setRemoving(row)}
            />
          </span>
        );
      },
    },
  ];

  return (
    <>
      <Note tone="warn" icon="lock">
        Sección exclusiva de Admin. No se puede degradar ni desactivar al único
        administrador activo, ni eliminar la propia cuenta.
      </Note>

      <Card pad={false}>
        <FilterBar>
          <SearchInput value={searchInput} onChange={setSearchInput}
            placeholder="Buscar por nombre o email…" width={280} />
        </FilterBar>

        <DataTable
          columns={columns}
          rows={list.data?.items ?? []}
          rowKey={(row) => row.id}
          loading={list.isLoading}
          rowTone={(row) => (row.isActive ? undefined : 'var(--surf2)')}
          empty={(
            <EmptyState
              title={search ? `Sin resultados para «${search}»` : 'Sin usuarios'}
              detail={search ? 'Prueba con otro término.' : undefined}
            />
          )}
        />

        {list.data ? <Pager data={list.data} onPage={setPage} /> : null}
      </Card>

      {creating ? <UserModal editing={null} onClose={() => setCreating(false)} /> : null}
      {editing ? <UserModal editing={editing} onClose={() => setEditing(null)} /> : null}
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
