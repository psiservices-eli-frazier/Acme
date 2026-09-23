import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { customersApi } from '../../api/resources';
import type { CustomerInput } from '../../api/types';
import { Field, FieldRow, FormActions } from '../../components/Field';
import { useFlash } from '../../components/Flash';
import { IconCheck, IconPlus } from '../../components/Icons';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../../components/Layout';
import { useAuthErrorRedirect } from '../../components/Loadable';

const EMPTY: CustomerInput = {
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  address: { line1: '', line2: '', city: '', state: '', postalCode: '', country: '' },
};

export function CustomerForm() {
  const { id } = useParams();
  const editing = id !== undefined;
  const customerId = editing ? Number(id) : undefined;

  const navigate = useNavigate();
  const flash = useFlash();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<CustomerInput>(EMPTY);
  const [errors, setErrors] = useState<ApiError | null>(null);

  const existing = useQuery({
    queryKey: ['customer', customerId],
    queryFn: () => customersApi.get(customerId!),
    enabled: editing,
  });

  useAuthErrorRedirect(existing.error);

  useEffect(() => {
    const customer = existing.data;
    if (!customer) return;
    setForm({
      firstName: customer.firstName,
      lastName: customer.lastName,
      email: customer.email,
      phone: customer.phone ?? '',
      address: {
        line1: customer.address.line1 ?? '',
        line2: customer.address.line2 ?? '',
        city: customer.address.city ?? '',
        state: customer.address.state ?? '',
        postalCode: customer.address.postalCode ?? '',
        country: customer.address.country ?? '',
      },
    });
  }, [existing.data]);

  usePageTitle(editing ? 'Edit customer' : 'New customer');

  const save = useMutation({
    mutationFn: (input: CustomerInput) =>
      editing ? customersApi.update(customerId!, input) : customersApi.create(input),
    onSuccess: async (customer) => {
      flash.setSuccess(`Customer '${customer.fullName}' was ${editing ? 'updated' : 'created'}.`);
      await queryClient.invalidateQueries({ queryKey: ['customers'] });
      await queryClient.invalidateQueries({ queryKey: ['customer', customer.id] });
      navigate(`/customers/${customer.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.isValidation) setErrors(error);
      else if (error instanceof ApiError && error.status === 403) navigate('/access-denied');
      else if (error instanceof ApiError && error.status === 401) navigate('/login');
      else flash.setError(error instanceof Error ? error.message : 'Something went wrong.');
    },
  });

  const error = (field: string) => errors?.fieldError(field);
  const setAddress = (patch: Partial<CustomerInput['address']>) =>
    setForm((current) => ({ ...current, address: { ...current.address, ...patch } }));

  return (
    <Layout active="customers">
      <PageHead>
        <div>
          <p className="crumb">
            <Link to="/customers">Customers</Link> / {editing ? existing.data?.fullName ?? '…' : 'New'}
          </p>
          <PageTitle>{editing ? 'Edit customer' : 'New customer'}</PageTitle>
        </div>
      </PageHead>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          setErrors(null);
          save.mutate({ ...form, phone: form.phone || null });
        }}
      >
        <Panel title="Contact">
          <FieldRow>
            <Field label="First name" error={error('firstName')}>
              <input
                type="text"
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.currentTarget.value })}
              />
            </Field>
            <Field label="Last name" error={error('lastName')}>
              <input
                type="text"
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.currentTarget.value })}
              />
            </Field>
          </FieldRow>
          <FieldRow>
            <Field label="Email" hint="Must be unique across all customers." error={error('email')}>
              <input
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.currentTarget.value })}
              />
            </Field>
            <Field label="Phone" error={error('phone')}>
              <input
                type="text"
                value={form.phone ?? ''}
                onChange={(e) => setForm({ ...form, phone: e.currentTarget.value })}
              />
            </Field>
          </FieldRow>
        </Panel>

        <Panel title="Address">
          <Field label="Address line 1" error={error('address.line1')}>
            <input
              type="text"
              value={form.address.line1 ?? ''}
              onChange={(e) => setAddress({ line1: e.currentTarget.value })}
            />
          </Field>
          <Field label="Address line 2" error={error('address.line2')}>
            <input
              type="text"
              value={form.address.line2 ?? ''}
              onChange={(e) => setAddress({ line2: e.currentTarget.value })}
            />
          </Field>
          <FieldRow>
            <Field label="City" error={error('address.city')}>
              <input
                type="text"
                value={form.address.city ?? ''}
                onChange={(e) => setAddress({ city: e.currentTarget.value })}
              />
            </Field>
            <Field label="State / region" error={error('address.state')}>
              <input
                type="text"
                value={form.address.state ?? ''}
                onChange={(e) => setAddress({ state: e.currentTarget.value })}
              />
            </Field>
            <Field label="Postal code" error={error('address.postalCode')}>
              <input
                type="text"
                value={form.address.postalCode ?? ''}
                onChange={(e) => setAddress({ postalCode: e.currentTarget.value })}
              />
            </Field>
            <Field label="Country" error={error('address.country')}>
              <input
                type="text"
                value={form.address.country ?? ''}
                onChange={(e) => setAddress({ country: e.currentTarget.value })}
              />
            </Field>
          </FieldRow>
        </Panel>

        <FormActions>
          <button type="submit" className="btn btn-primary" disabled={save.isPending}>
            {editing ? (
              <>
                <IconCheck /> Save changes
              </>
            ) : (
              <>
                <IconPlus /> Create customer
              </>
            )}
          </button>
          <Link className="btn btn-quiet" to={editing ? `/customers/${customerId}` : '/customers'}>
            Cancel
          </Link>
        </FormActions>
      </form>
    </Layout>
  );
}
