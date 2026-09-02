import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/problem';
import { roles as rolesApi } from '../api/endpoints';
import type { PermissionCatalogResponse, PermissionKey, RoleResponse } from '../api/types';
import { useAuth } from '../auth/useAuth';
import { P } from '../auth/permissions';
import { Field, Input, TextArea } from '../components/Field';
import { Icon } from '../components/Icon';
import { PermissionMatrix } from '../components/PermissionMatrix';
import { usePermissionCatalog } from '../components/usePermissionCatalog';
import { useToast } from '../components/useToast';
import { Bar, Button, Card, CardHead, Chip, EmptyState, Note, Spinner } from '../components/ui';
import { usePageMeta } from '../lib/hooks';

/**
 * Pantalla contenedora: sólo carga. El formulario va aparte y se remonta con
 * `key`, de modo que su estado inicial sale de las props y no hace falta un
 * efecto que lo siembre.
 */
export function RoleEditor() {
  const { id = '' } = useParams();
  const { can } = useAuth();

  const readOnly = !can(P.roles.edit);

  const role = useQuery({
    queryKey: ['roles', id],
    queryFn: () => rolesApi.get(id),
    retry: (count, error) => !(error instanceof ApiError && error.isNotFound) && count < 2,
  });
  const catalog = usePermissionCatalog();

  usePageMeta({
    title: role.data?.name ?? 'Rol',
    subtitle: readOnly ? 'Sólo lectura' : 'Editor de permisos',
  }, [role.data?.id, readOnly]);

  if (role.isLoading || catalog.isLoading) {
    return <Card><Spinner label="Cargando rol…" /></Card>;
  }

  if (role.error instanceof ApiError && role.error.isNotFound) {
    return (
      <Card>
        <EmptyState
          icon="alert"
          title="Rol no encontrado"
          detail="El rol no existe o fue eliminado."
          action={<Link to="/roles" style={{ fontSize: 12.5 }}>Volver a Roles</Link>}
        />
      </Card>
    );
  }

  if (!role.data || !catalog.data) return null;

  return (
    <RoleForm
      // Al cambiar de rol o al recargar tras guardar, el formulario se remonta
      // con los valores nuevos en lugar de sincronizarse con un efecto.
      key={`${role.data.id}:${role.dataUpdatedAt}`}
      role={role.data}
      catalog={catalog.data}
      readOnly={readOnly}
    />
  );
}

function RoleForm({
  role: data, catalog, readOnly,
}: {
  role: RoleResponse;
  catalog: PermissionCatalogResponse;
  readOnly: boolean;
}) {
  const toast = useToast();
  const queryClient = useQueryClient();

  const [name, setName] = useState(data.name);
  const [description, setDescription] = useState(data.description ?? '');
  const [granted, setGranted] = useState<Set<PermissionKey>>(() => new Set(data.permissions));
  const [error, setError] = useState<ApiError | null>(null);

  const total = catalog.totalCount;

  const dirty = useMemo(() => {
    const original = new Set(data.permissions);
    return name !== data.name
      || description !== (data.description ?? '')
      || original.size !== granted.size
      || [...granted].some((key) => !original.has(key));
  }, [data, name, description, granted]);

  const save = useMutation({
    mutationFn: () => rolesApi.update(data.id, {
      name: name.trim(),
      description: description.trim() || null,
      // Se envía en el orden que devuelve el catálogo; la API lo normaliza igual.
      permissions: [...granted],
    }),
    onSuccess: (updated) => {
      toast.success('Permisos guardados',
        `${updated.name} · ${updated.permissionCount} de ${total} permisos`);
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      // El propio usuario puede haber cambiado sus permisos: se refresca el perfil.
      void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
    },
    onError: (caught) => {
      if (caught instanceof ApiError && (caught.isValidation || caught.isConflict)) {
        setError(caught);
        if (caught.isConflict) toast.fromError(caught);
      } else {
        toast.fromError(caught, 'No se pudieron guardar los permisos');
      }
    },
  });

  const setAll = (on: boolean) =>
    setGranted(on ? new Set(catalog.permissions.map((p) => p.key)) : new Set());

  return (
    <>
      <Link
        to="/roles"
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          fontSize: 12.5, color: 'var(--ink2)', alignSelf: 'flex-start',
        }}
      >
        <Icon name="left" size={14} />
        Volver a Roles
      </Link>

      <Card>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 20, flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: 260 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 9, flexWrap: 'wrap' }}>
              {data.isSystem
                ? <Chip tone="acc" icon="lock">rol de sistema</Chip>
                : <Chip tone="neutral" icon="lock">rol personalizado</Chip>}
              <Chip tone={data.userCount > 0 ? 'acc' : 'neutral'} icon="users">
                {data.userCount} {data.userCount === 1 ? 'usuario' : 'usuarios'}
              </Chip>
            </div>
            <h2 style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.03em', margin: '9px 0 0' }}>
              {data.name}
            </h2>
          </div>
        </div>

        <div style={{ height: 1, background: 'var(--bord)', margin: '16px 0' }} />

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <Field label="Nombre del rol" width={300} required error={error?.fieldError('name')}>
            <Input value={name} onChange={setName} disabled={readOnly}
              error={Boolean(error?.fieldError('name'))} />
          </Field>
          <Field label="Descripción" error={error?.fieldError('description')}>
            <TextArea value={description} onChange={setDescription} rows={1} />
          </Field>
        </div>
      </Card>

      {data.isSystem ? (
        <Note tone="warn" icon="lock">
          Es el <strong>rol de sistema</strong>: no se puede eliminar ni dejar sin los permisos
          de administrar roles y usuarios. Es el último recurso para recuperar el acceso.
        </Note>
      ) : null}

      {error?.fieldError('permissions') ? (
        <Note tone="danger" icon="alert">{error.fieldError('permissions')}</Note>
      ) : null}

      <Card pad={false}>
        <CardHead
          title="Permisos"
          sub="Marca lo que este rol puede hacer. Se guarda al confirmar."
          right={(
            <span style={{ display: 'flex', alignItems: 'center', gap: 9, flexWrap: 'wrap' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: 9, minWidth: 150 }}>
                <Bar value={granted.size} max={Math.max(1, total)} color="var(--acc)" />
                <span style={{ fontSize: 11.5, color: 'var(--ink2)', whiteSpace: 'nowrap' }}>
                  <span
                    className="num"
                    style={{ color: 'var(--acc)', fontWeight: 600, fontSize: 13 }}
                  >
                    {granted.size}
                  </span>
                  {' de '}
                  <span className="num">{total}</span>
                </span>
              </span>
              {readOnly ? null : (
                <>
                  <Button icon="check" size="sm" onClick={() => setAll(true)}>Marcar todo</Button>
                  <Button icon="x" size="sm" onClick={() => setAll(false)}>Desmarcar todo</Button>
                </>
              )}
            </span>
          )}
        />
        <PermissionMatrix
          catalog={catalog}
          granted={granted}
          onChange={setGranted}
          readOnly={readOnly}
        />
      </Card>

      <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start', flexWrap: 'wrap' }}>
        <Note tone="acc">
          Marcar <strong>Crear</strong>, <strong>Editar</strong> o <strong>Eliminar</strong> marca
          también <strong>Ver</strong>: sin ella la pantalla no se puede abrir y el permiso
          quedaría inalcanzable. Quitar <strong>Ver</strong> quita todo el módulo.
        </Note>

        {readOnly ? (
          <Note tone="neutral" icon="lock">
            Sólo lectura: te falta el permiso <code style={{ fontFamily: 'var(--mono)' }}>
            roles.edit</code>.
          </Note>
        ) : (
          <div style={{ display: 'flex', gap: 9, flexShrink: 0, marginLeft: 'auto' }}>
            <Button
              onClick={() => {
                setName(data.name);
                setDescription(data.description ?? '');
                setGranted(new Set(data.permissions));
                setError(null);
              }}
              disabled={!dirty}
            >
              Descartar cambios
            </Button>
            <Button
              kind="primary"
              icon="check"
              onClick={() => { setError(null); save.mutate(); }}
              disabled={!dirty || !name.trim()}
              loading={save.isPending}
            >
              Guardar permisos
            </Button>
          </div>
        )}
      </div>
    </>
  );
}
