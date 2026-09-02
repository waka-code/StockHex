import { useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/problem';
import type { PagedResponse } from '../api/types';
import { useCurrentUser } from '../auth/useAuth';
import { can } from '../auth/roles';
import { DataTable, Pager, type Column } from '../components/DataTable';
import { FilterBar, SearchInput } from '../components/Field';
import { ConfirmModal, Modal } from '../components/Modal';
import { useToast } from '../components/useToast';
import { Button, Card, EmptyState } from '../components/ui';
import { useDebounced, usePageMeta, useResetPageOnFilterChange } from '../lib/hooks';

/**
 * Categorías, Proveedores y Clientes son el mismo patrón: tabla paginada con
 * búsqueda y un modal de alta o edición. Se resuelve una vez y se parametriza,
 * en lugar de escribir tres pantallas casi idénticas.
 */
export interface CrudConfig<TItem, TForm> {
  /** Plural, para el título: "Clientes". */
  title: string;
  /** Singular en minúscula, para los mensajes: "cliente". */
  singular: string;
  /** Género gramatical, para concordar los mensajes en español. */
  gender: 'm' | 'f';
  searchPlaceholder: string;
  queryKey: string;
  list: (query: { page: number; pageSize: number; search?: string }) => Promise<PagedResponse<TItem>>;
  create: (form: TForm) => Promise<TItem>;
  update: (id: string, form: TForm) => Promise<TItem>;
  remove: (id: string) => Promise<void>;
  columns: (helpers: {
    edit: (item: TItem) => void;
    remove: (item: TItem) => void;
    writable: boolean;
  }) => Column<TItem>[];
  rowKey: (item: TItem) => string;
  itemLabel: (item: TItem) => string;
  /** Filas dependientes que impiden el borrado, para avisarlo antes de intentarlo. */
  blockingCount?: (item: TItem) => number;
  blockingLabel?: string;
  emptyForm: TForm;
  toForm: (item: TItem) => TForm;
  isValid: (form: TForm) => boolean;
  renderForm: (
    form: TForm,
    set: <K extends keyof TForm>(key: K, value: TForm[K]) => void,
    error: ApiError | null,
  ) => ReactNode;
  note?: ReactNode;
  formWidth?: number;
}

const FORM_ID = 'crud-form';

export function CrudPage<TItem, TForm>(config: CrudConfig<TItem, TForm>) {
  const user = useCurrentUser();
  const toast = useToast();
  const queryClient = useQueryClient();
  const writable = can.manageCatalog(user.role);

  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);
  const [editing, setEditing] = useState<TItem | null>(null);
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<TItem | null>(null);

  useResetPageOnFilterChange(search, page, setPage);

  const query = { page, pageSize: 20, search: search || undefined };
  const list = useQuery({
    queryKey: [config.queryKey, query],
    queryFn: () => config.list(query),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: [config.queryKey] });
    // Los contadores de productos por categoría o proveedor cambian con esto.
    void queryClient.invalidateQueries({ queryKey: ['products'] });
  };

  const remove = useMutation({
    mutationFn: (id: string) => config.remove(id),
    onSuccess: () => {
      const article = config.gender === 'f' ? 'eliminada' : 'eliminado';
      toast.success(`${config.singular[0].toUpperCase()}${config.singular.slice(1)} ${article}`);
      invalidate();
      setRemoving(null);
    },
    onError: (caught) => {
      toast.fromError(caught, 'No se pudo eliminar');
      setRemoving(null);
    },
  });

  usePageMeta({
    title: config.title,
    subtitle: list.data
      ? `${list.data.totalCount} ${list.data.totalCount === 1 ? config.singular : config.title.toLowerCase()}`
      : undefined,
    actions: writable ? (
      <Button kind="primary" icon="plus" onClick={() => setCreating(true)}>
        {config.gender === 'f' ? 'Nueva' : 'Nuevo'} {config.singular}
      </Button>
    ) : undefined,
  }, [list.data?.totalCount, writable]);

  const columns = config.columns({
    writable,
    edit: setEditing,
    remove: setRemoving,
  });

  const blocking = removing && config.blockingCount ? config.blockingCount(removing) : 0;

  return (
    <>
      {config.note ?? null}

      <Card pad={false}>
        <FilterBar>
          <SearchInput
            value={searchInput}
            onChange={setSearchInput}
            placeholder={config.searchPlaceholder}
            width={280}
          />
        </FilterBar>

        <DataTable
          columns={columns}
          rows={list.data?.items ?? []}
          rowKey={config.rowKey}
          loading={list.isLoading}
          empty={(
            <EmptyState
              title={search
                ? `Sin resultados para «${search}»`
                : `Todavía no hay ${config.title.toLowerCase()}`}
              detail={search
                ? 'Prueba con otro término.'
                : `Crea ${config.gender === 'f' ? 'la primera' : 'el primero'} para empezar.`}
              action={search ? (
                <Button icon="x" size="sm" onClick={() => setSearchInput('')}>Limpiar búsqueda</Button>
              ) : writable ? (
                <Button kind="primary" icon="plus" size="sm" onClick={() => setCreating(true)}>
                  {config.gender === 'f' ? 'Nueva' : 'Nuevo'} {config.singular}
                </Button>
              ) : undefined}
            />
          )}
        />

        {list.data ? <Pager data={list.data} onPage={setPage} /> : null}
      </Card>

      {creating || editing ? (
        <CrudModal
          config={config}
          editing={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={invalidate}
        />
      ) : null}

      {removing ? (
        <ConfirmModal
          title={`Eliminar ${config.singular}`}
          confirmLabel="Eliminar"
          loading={remove.isPending}
          onClose={() => setRemoving(null)}
          onConfirm={() => remove.mutate(config.rowKey(removing))}
          message={(
            <>
              Se eliminará <strong>{config.itemLabel(removing)}</strong>.
              {blocking > 0 ? (
                <div style={{ marginTop: 8, color: 'var(--dang)' }}>
                  Tiene {blocking} {config.blockingLabel} asociado{blocking === 1 ? '' : 's'},
                  así que la API va a rechazar el borrado con un 409.
                </div>
              ) : null}
            </>
          )}
        />
      ) : null}
    </>
  );
}

function CrudModal<TItem, TForm>({
  config, editing, onClose, onSaved,
}: {
  config: CrudConfig<TItem, TForm>;
  editing: TItem | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [error, setError] = useState<ApiError | null>(null);
  const [form, setForm] = useState<TForm>(() => (editing ? config.toForm(editing) : config.emptyForm));

  const set = <K extends keyof TForm>(key: K, value: TForm[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const save = useMutation({
    mutationFn: () => (editing
      ? config.update(config.rowKey(editing), form)
      : config.create(form)),
    onSuccess: () => {
      const article = config.gender === 'f'
        ? (editing ? 'actualizada' : 'creada')
        : (editing ? 'actualizado' : 'creado');
      toast.success(`${config.singular[0].toUpperCase()}${config.singular.slice(1)} ${article}`);
      onSaved();
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

  const capitalized = config.singular[0].toUpperCase() + config.singular.slice(1);

  return (
    <Modal
      title={editing
        ? `Editar ${config.singular}`
        : `${config.gender === 'f' ? 'Nueva' : 'Nuevo'} ${config.singular}`}
      subtitle={editing ? config.itemLabel(editing) : `${capitalized} nuev${config.gender === 'f' ? 'a' : 'o'}`}
      onClose={onClose}
      width={config.formWidth ?? 460}
      footer={(
        <span style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Button onClick={onClose}>Cancelar</Button>
          <Button
            kind="primary"
            type="submit"
            form={FORM_ID}
            disabled={!config.isValid(form)}
            loading={save.isPending}
          >
            Guardar
          </Button>
        </span>
      )}
    >
      <form
        id={FORM_ID}
        onSubmit={(event) => { event.preventDefault(); setError(null); save.mutate(); }}
        style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
      >
        {config.renderForm(form, set, error)}
      </form>
    </Modal>
  );
}
