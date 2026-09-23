import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { productsApi } from '../../api/resources';
import type { ProductInput } from '../../api/types';
import { CheckField, Field, FieldRow, FormActions } from '../../components/Field';
import { useFlash } from '../../components/Flash';
import { IconCheck, IconPlus } from '../../components/Icons';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../../components/Layout';
import { useAuthErrorRedirect } from '../../components/Loadable';

const EMPTY: ProductInput = {
  sku: '',
  name: '',
  description: '',
  price: null,
  stockQuantity: 0,
  active: true,
};

export function ProductForm() {
  const { id } = useParams();
  const editing = id !== undefined;
  const productId = editing ? Number(id) : undefined;

  const navigate = useNavigate();
  const flash = useFlash();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<ProductInput>(EMPTY);
  const [errors, setErrors] = useState<ApiError | null>(null);

  const existing = useQuery({
    queryKey: ['product', productId],
    queryFn: () => productsApi.get(productId!),
    enabled: editing,
  });

  useAuthErrorRedirect(existing.error);

  useEffect(() => {
    const product = existing.data;
    if (!product) return;
    setForm({
      sku: product.sku,
      name: product.name,
      description: product.description ?? '',
      price: product.price,
      stockQuantity: product.stockQuantity,
      active: product.active,
    });
  }, [existing.data]);

  usePageTitle(editing ? 'Edit product' : 'New product');

  const save = useMutation({
    mutationFn: (input: ProductInput) =>
      editing ? productsApi.update(productId!, input) : productsApi.create(input),
    onSuccess: async (product) => {
      flash.setSuccess(`Product '${product.name}' was ${editing ? 'updated' : 'created'}.`);
      await queryClient.invalidateQueries({ queryKey: ['products'] });
      await queryClient.invalidateQueries({ queryKey: ['product', product.id] });
      navigate(`/products/${product.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.isValidation) {
        // Field-level errors, including the duplicate-SKU refusal, render next to
        // the input rather than as a banner.
        setErrors(error);
      } else if (error instanceof ApiError && error.status === 403) {
        navigate('/access-denied');
      } else if (error instanceof ApiError && error.status === 401) {
        navigate('/login');
      } else {
        flash.setError(error instanceof Error ? error.message : 'Something went wrong.');
      }
    },
  });

  const error = (field: string) => errors?.fieldError(field);

  return (
    <Layout active="products">
      <PageHead>
        <div>
          <p className="crumb">
            <Link to="/products">Products</Link> / {editing ? existing.data?.sku ?? '…' : 'New'}
          </p>
          <PageTitle>{editing ? 'Edit product' : 'New product'}</PageTitle>
        </div>
      </PageHead>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          setErrors(null);
          save.mutate({ ...form, description: form.description || null });
        }}
      >
        <Panel title="Details">
          <FieldRow>
            <Field
              label="SKU"
              hint="Must be unique across all products."
              error={error('sku')}
            >
              <input
                type="text"
                placeholder="BX-WIDGET-01"
                value={form.sku}
                onChange={(e) => setForm({ ...form, sku: e.currentTarget.value })}
              />
            </Field>
            <Field label="Name" error={error('name')}>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.currentTarget.value })}
              />
            </Field>
          </FieldRow>

          <Field label="Description" error={error('description')}>
            <textarea
              value={form.description ?? ''}
              onChange={(e) => setForm({ ...form, description: e.currentTarget.value })}
            />
          </Field>

          <FieldRow>
            <Field label="Price" error={error('price')}>
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.price ?? ''}
                onChange={(e) =>
                  setForm({ ...form, price: e.currentTarget.value === '' ? null : Number(e.currentTarget.value) })
                }
              />
            </Field>
            <Field label="Stock quantity" error={error('stockQuantity')}>
              <input
                type="number"
                min="0"
                value={form.stockQuantity ?? ''}
                onChange={(e) =>
                  setForm({
                    ...form,
                    stockQuantity: e.currentTarget.value === '' ? null : Number(e.currentTarget.value),
                  })
                }
              />
            </Field>
          </FieldRow>

          <CheckField
            label="Active"
            hint="Only active products can be added to new order lines."
            checked={form.active}
            onChange={(active) => setForm({ ...form, active })}
          />
        </Panel>

        <FormActions>
          <button type="submit" className="btn btn-primary" disabled={save.isPending}>
            {editing ? (
              <>
                <IconCheck /> Save changes
              </>
            ) : (
              <>
                <IconPlus /> Create product
              </>
            )}
          </button>
          <Link className="btn btn-quiet" to={editing ? `/products/${productId}` : '/products'}>
            Cancel
          </Link>
        </FormActions>
      </form>
    </Layout>
  );
}
