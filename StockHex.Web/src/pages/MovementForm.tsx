import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/problem';
import { clients, movements, products, suppliers } from '../api/endpoints';
import type { CreateMovementRequest, MovementType, ProductResponse } from '../api/types';
import { Field, Input, Select, TextArea } from '../components/Field';
import { Icon } from '../components/Icon';
import { Modal } from '../components/Modal';
import { Button, Note } from '../components/ui';
import { MOVEMENT } from '../components/tokens';
import { useToast } from '../components/useToast';
import { clp } from '../lib/format';

/** El botón de enviar vive en el pie del modal, fuera del <form>. */
const FORM_ID = 'movement-form';

const TYPES: { value: MovementType; desc: string }[] = [
  { value: 'In', desc: 'Suma al stock' },
  { value: 'Out', desc: 'Resta del stock' },
  { value: 'Adjustment', desc: 'Fija el stock del conteo' },
];

function TypePicker({
  value, onChange }: { value: MovementType; onChange: (type: MovementType) => void }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <span style={{ fontSize: 11, fontWeight: 500, color: 'var(--ink2)' }}>Tipo de movimiento</span>
      <div style={{ display: 'flex', gap: 9, flexWrap: 'wrap' }}>
        {TYPES.map((type) => {
          const meta = MOVEMENT[type.value];
          const selected = value === type.value;
          return (
            <button
              key={type.value}
              type="button"
              onClick={() => onChange(type.value)}
              aria-pressed={selected}
              style={{
                flex: '1 1 140px', padding: '11px 12px', textAlign: 'left',
                display: 'flex', flexDirection: 'column', gap: 5,
                borderRadius: 7, cursor: 'pointer',
                background: selected ? `var(--${type.value === 'In' ? 'in' : type.value === 'Out' ? 'out' : 'adj'}-bg)` : 'var(--surf)',
                border: `1px solid ${selected ? meta.color : 'var(--bord2)'}`,
                boxShadow: selected ? `0 0 0 2px ${meta.color}33` : undefined }}
            >
              <span
                style={{
                  display: 'flex', alignItems: 'center', gap: 7,
                  fontSize: 13, fontWeight: 600,
                  color: selected ? meta.color : 'var(--ink2)' }}
              >
                <Icon name={meta.icon} size={15} />
                {meta.label}
              </span>
              <span style={{ fontSize: 11, color: 'var(--ink3)', lineHeight: 1.45 }}>
                {type.desc}
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

interface Props {
  onClose: () => void;
  /** Preselecciona el producto cuando se abre desde su detalle. */
  product?: ProductResponse;
}

export function MovementForm({ onClose, product }: Props) {
  const toast = useToast();
  const queryClient = useQueryClient();

  const [productId, setProductId] = useState(product?.id ?? '');
  const [type, setType] = useState<MovementType>('In');
  const [quantity, setQuantity] = useState('');
  const [unitPrice, setUnitPrice] = useState('');
  const [partyKind, setPartyKind] = useState<'none' | 'supplier' | 'client'>('none');
  const [partyId, setPartyId] = useState('');
  const [comment, setComment] = useState('');
  const [error, setError] = useState<ApiError | null>(null);

  // Sólo se cargan las listas si no venía un producto fijado.
  const productList = useQuery({
    queryKey: ['products', 'picker'],
    queryFn: () => products.list({ page: 1, pageSize: 100, isActive: true }),
    enabled: !product });

  const supplierList = useQuery({
    queryKey: ['suppliers', 'picker'],
    queryFn: () => suppliers.list({ page: 1, pageSize: 100 }),
    enabled: partyKind === 'supplier' });

  const clientList = useQuery({
    queryKey: ['clients', 'picker'],
    queryFn: () => clients.list({ page: 1, pageSize: 100 }),
    enabled: partyKind === 'client' });

  const selected = product ?? productList.data?.items.find((p) => p.id === productId);
  const qty = Number(quantity);
  const hasQty = quantity !== '' && Number.isFinite(qty);

  // El stock resultante se anticipa con la misma regla que aplica la API, para
  // avisar antes de enviar en lugar de esperar un 409.
  const projected = useMemo(() => {
    if (!selected || !hasQty) return null;
    if (type === 'In') return selected.stockQuantity + qty;
    if (type === 'Out') return selected.stockQuantity - qty;
    return qty;
  }, [selected, hasQty, qty, type]);

  const insufficient = type === 'Out' && projected !== null && projected < 0;

  const create = useMutation({
    mutationFn: (body: CreateMovementRequest) => movements.create(body),
    onSuccess: (movement) => {
      toast.success(
        'Movimiento registrado',
        `${MOVEMENT[movement.movementType].label} de ${movement.quantity} · ${movement.productSku} · stock ${movement.stockAfter}`,
      );
      // Se invalidan productos, movimientos y reportes: los tres cambian.
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      void queryClient.invalidateQueries({ queryKey: ['movements'] });
      void queryClient.invalidateQueries({ queryKey: ['reports'] });
      onClose();
    },
    onError: (caught) => {
      if (caught instanceof ApiError && (caught.isValidation || caught.isConflict)) {
        setError(caught);
        if (caught.isConflict) toast.fromError(caught);
      } else {
        toast.fromError(caught, 'No se pudo registrar el movimiento');
      }
    } });

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    create.mutate({
      productId,
      movementType: type,
      quantity: qty,
      unitPrice: unitPrice === '' ? null : Number(unitPrice),
      clientId: partyKind === 'client' && partyId ? partyId : null,
      supplierId: partyKind === 'supplier' && partyId ? partyId : null,
      comment: comment.trim() || null });
  };

  const canSubmit = Boolean(productId) && hasQty && !insufficient
    && (type === 'Adjustment' ? qty >= 0 : qty > 0);

  return (
    <Modal
      title="Registrar movimiento"
      subtitle="Queda a tu nombre y no se puede editar después"
      onClose={onClose}
      width={600}
      footer={(
        <>
          {projected !== null ? (
            <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
              Stock resultante:{' '}
              <span
                className="num"
                style={{ color: projected < 0 ? 'var(--dang)' : 'var(--ink)', fontWeight: 500 }}
              >
                {projected}
              </span>
            </span>
          ) : null}
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 9 }}>
            <Button onClick={onClose}>Cancelar</Button>
            <Button
              kind="primary"
              type="submit"
              form={FORM_ID}
              disabled={!canSubmit}
              loading={create.isPending}
            >
              Registrar movimiento
            </Button>
          </span>
        </>
      )}
    >
      <form id={FORM_ID} onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {product ? (
          <Note tone="neutral" icon="box">
            <strong>{product.sku}</strong> · {product.name} · stock actual{' '}
            <span className="num">{product.stockQuantity}</span>
          </Note>
        ) : (
          <Field label="Producto" width="100%" required error={error?.fieldError('productId')}>
            <Select
              value={productId}
              onChange={setProductId}
              placeholder={productList.isLoading ? 'Cargando productos…' : 'Selecciona un producto'}
              error={Boolean(error?.fieldError('productId'))}
              options={(productList.data?.items ?? []).map((p) => ({
                value: p.id,
                label: `${p.sku} · ${p.name} (stock ${p.stockQuantity})` }))}
            />
          </Field>
        )}

        <TypePicker value={type} onChange={setType} />

        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <Field
            label={type === 'Adjustment' ? 'Stock final' : 'Cantidad'}
            width={150}
            required
            error={error?.fieldError('quantity')}
            hint={type === 'Adjustment' ? 'el conteo real' : 'unidades'}
          >
            <Input
              type="number"
              min={0}
              value={quantity}
              onChange={setQuantity}
              placeholder="0"
              error={Boolean(error?.fieldError('quantity')) || insufficient}
            />
          </Field>

          <Field
            label="Precio unitario"
            width={170}
            error={error?.fieldError('unitPrice')}
            hint="opcional"
          >
            <Input type="number" min={0} value={unitPrice} onChange={setUnitPrice} placeholder="—" />
          </Field>

          <Field label="Contraparte" hint="proveedor o cliente, no ambos">
            <Select
              value={partyKind}
              onChange={(value) => {
                setPartyKind(value as typeof partyKind);
                setPartyId('');
              }}
              options={[
                { value: 'none', label: 'Sin contraparte' },
                { value: 'supplier', label: 'Proveedor' },
                { value: 'client', label: 'Cliente' },
              ]}
            />
          </Field>
        </div>

        {partyKind !== 'none' ? (
          <Field
            label={partyKind === 'supplier' ? 'Proveedor' : 'Cliente'}
            width="100%"
            error={error?.fieldError(partyKind === 'supplier' ? 'supplierId' : 'clientId')}
          >
            <Select
              value={partyId}
              onChange={setPartyId}
              placeholder="Selecciona…"
              options={((partyKind === 'supplier' ? supplierList.data : clientList.data)?.items ?? [])
                .map((item) => ({ value: item.id, label: item.name }))}
            />
          </Field>
        ) : null}

        {insufficient && selected ? (
          <Note tone="danger" icon="alert">
            <strong>Stock insuficiente.</strong> Disponible{' '}
            <span className="num">{selected.stockQuantity}</span>, solicitado{' '}
            <span className="num">{qty}</span>.
            <div style={{ color: 'var(--ink2)', marginTop: 3, fontSize: 11.5 }}>
              Registra primero una entrada, o baja la cantidad a {selected.stockQuantity} o menos.
            </div>
          </Note>
        ) : null}

        {type === 'Adjustment' && selected && hasQty && !insufficient ? (
          <Note tone="adj" icon="filter">
            Un ajuste <strong>fija</strong> el stock en {qty}: la variación registrada será de{' '}
            <span className="num">{qty - selected.stockQuantity >= 0 ? '+' : ''}{qty - selected.stockQuantity}</span>{' '}
            unidades respecto de las {selected.stockQuantity} actuales.
          </Note>
        ) : null}

        {unitPrice !== '' && hasQty ? (
          <div style={{ fontSize: 11.5, color: 'var(--ink3)' }}>
            Total del movimiento: <span className="num">{clp(Number(unitPrice) * qty)}</span>
          </div>
        ) : null}

        <Field label="Comentario" width="100%" error={error?.fieldError('comment')}>
          <TextArea
            value={comment}
            onChange={setComment}
            placeholder="Nº de orden de compra, pedido, motivo del ajuste…"
            rows={2}
          />
        </Field>

        <Note tone="neutral">
          Cliente y proveedor son excluyentes: una salida a cliente es una venta,
          una salida a proveedor es una devolución.
        </Note>
      </form>
    </Modal>
  );
}

/** Botón que abre el formulario. Vive aquí para no repetir el estado del modal. */
export function NewMovementButton({ product }: { product?: ProductResponse }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button kind="primary" icon="plus" onClick={() => setOpen(true)}>
        Registrar movimiento
      </Button>
      {open ? <MovementForm onClose={() => setOpen(false)} product={product} /> : null}
    </>
  );
}
