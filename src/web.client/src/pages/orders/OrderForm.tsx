import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { customersApi, ordersApi, productsApi } from '../../api/resources';
import type { OrderInput, OrderStatus } from '../../api/types';
import { Field, FormActions } from '../../components/Field';
import { useFlash } from '../../components/Flash';
import { IconCheck, IconPlus } from '../../components/Icons';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../../components/Layout';
import { useAuthErrorRedirect } from '../../components/Loadable';
import { OrderLineEditor, newLine } from './OrderLineEditor';
import type { EditableLine } from './OrderLineEditor';

export function OrderForm() {
  const { id } = useParams();
  const editing = id !== undefined;
  const orderId = editing ? Number(id) : undefined;

  const navigate = useNavigate();
  const flash = useFlash();
  const queryClient = useQueryClient();

  const [customerId, setCustomerId] = useState<number | null>(null);
  const [status, setStatus] = useState<OrderStatus>('NEW');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<EditableLine[]>([]);
  const [errors, setErrors] = useState<ApiError | null>(null);

  const customers = useQuery({ queryKey: ['customer-options'], queryFn: customersApi.options });
  const products = useQuery({ queryKey: ['product-options'], queryFn: productsApi.options });
  const statuses = useQuery({ queryKey: ['order-statuses'], queryFn: ordersApi.statuses });

  const existing = useQuery({
    queryKey: ['order', orderId],
    queryFn: () => ordersApi.get(orderId!),
    enabled: editing,
  });

  useAuthErrorRedirect(existing.error ?? customers.error ?? products.error);

  useEffect(() => {
    const order = existing.data;
    if (!order) return;
    setCustomerId(order.customer?.id ?? null);
    setStatus(order.status);
    setNotes(order.notes ?? '');
    setLines(order.items.map((item) => ({ ...newLine(), productId: item.productId, quantity: item.quantity })));
  }, [existing.data]);

  usePageTitle(editing ? `Edit order ${existing.data?.orderNumber ?? ''}`.trim() : 'New order');

  const save = useMutation({
    mutationFn: (input: OrderInput) => (editing ? ordersApi.update(orderId!, input) : ordersApi.create(input)),
    onSuccess: async (order) => {
      flash.setSuccess(`Order ${order.orderNumber} was ${editing ? 'updated' : 'created'}.`);
      await queryClient.invalidateQueries({ queryKey: ['orders'] });
      await queryClient.invalidateQueries({ queryKey: ['order', order.id] });
      navigate(`/orders/${order.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.isValidation) setErrors(error);
      else if (error instanceof ApiError && error.status === 403) navigate('/access-denied');
      else if (error instanceof ApiError && error.status === 401) navigate('/login');
      else flash.setError(error instanceof Error ? error.message : 'Something went wrong.');
    },
  });

  const error = (field: string) => errors?.fieldError(field);
  const lineError = (index: number, field: 'productId' | 'quantity') => error(`Items[${index}].${field}`);

  return (
    <Layout active="orders">
      <PageHead>
        <div>
          <p className="crumb">
            <Link to="/orders">Orders</Link> / {editing ? existing.data?.orderNumber ?? '…' : 'New'}
          </p>
          <PageTitle>
            {editing ? `Edit order ${existing.data?.orderNumber ?? ''}`.trim() : 'New order'}
          </PageTitle>
        </div>
      </PageHead>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          setErrors(null);
          save.mutate({
            customerId,
            status,
            notes: notes || null,
            items: lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
          });
        }}
      >
        <Panel title="Summary">
          <Field label="Customer" error={error('customerId')}>
            <select
              value={customerId ?? ''}
              onChange={(e) => setCustomerId(e.currentTarget.value === '' ? null : Number(e.currentTarget.value))}
            >
              <option value="">— select a customer —</option>
              {(customers.data ?? []).map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {customer.displayName}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Status" error={error('status')}>
            <select value={status} onChange={(e) => setStatus(e.currentTarget.value as OrderStatus)}>
              {(statuses.data ?? []).map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </Field>

          <Field label="Notes" error={error('notes')}>
            <textarea value={notes} onChange={(e) => setNotes(e.currentTarget.value)} />
          </Field>
        </Panel>

        <Panel title="Line items">
          {/* Collection-level slot: this is where "an order needs at least one line
              item" surfaces, as distinct from a problem with a particular row. */}
          {error('items') && <p className="field-error">{error('items')}</p>}

          <OrderLineEditor
            lines={lines}
            products={products.data ?? []}
            onChange={setLines}
            errorFor={lineError}
          />

          <p className="hint" style={{ marginTop: 12 }}>
            Each line's unit price is taken from the product's current price when the line is first added,
            and is preserved on later edits.
          </p>
        </Panel>

        <FormActions>
          <button type="submit" className="btn btn-primary" disabled={save.isPending}>
            {editing ? (
              <>
                <IconCheck /> Save changes
              </>
            ) : (
              <>
                <IconPlus /> Create order
              </>
            )}
          </button>
          <Link className="btn btn-quiet" to={editing ? `/orders/${orderId}` : '/orders'}>
            Cancel
          </Link>
        </FormActions>
      </form>
    </Layout>
  );
}
