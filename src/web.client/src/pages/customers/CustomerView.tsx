import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { customersApi } from '../../api/resources';
import { AdminOnly } from '../../auth/AuthContext';
import { dateTime } from '../../format';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconTrash } from '../../components/Icons';
import { Layout, Muted, PageHead, PageTitle, Panel, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';

export function CustomerView() {
  const customerId = Number(useParams().id);
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['customer', customerId],
    queryFn: () => customersApi.get(customerId),
  });

  usePageTitle(query.data?.fullName ?? 'Customer');

  return (
    <Layout active="customers">
      <Loadable query={query}>
        {(customer) => (
          <>
            <PageHead>
              <div>
                <p className="crumb">
                  <Link to="/customers">Customers</Link> / {customer.fullName}
                </p>
                <PageTitle badge={<IconBadge entity="customer" />}>{customer.fullName}</PageTitle>
              </div>
              <AdminOnly>
                <div style={{ display: 'flex', gap: 10 }}>
                  <Link className="btn" to={`/customers/${customer.id}/edit`}>
                    <IconEdit /> Edit
                  </Link>
                  <button
                    type="button"
                    className="btn btn-danger"
                    onClick={() =>
                      void remove({
                        confirm: 'Delete this customer? This cannot be undone.',
                        run: () => customersApi.remove(customer.id),
                        success: 'Customer was deleted.',
                        listPath: '/customers',
                        invalidate: ['customers'],
                      })
                    }
                  >
                    <IconTrash /> Delete
                  </button>
                </div>
              </AdminOnly>
            </PageHead>

            <Panel title="Contact">
              <dl className="detail">
                <dt>Email</dt>
                <dd>{customer.email}</dd>
                <dt>Phone</dt>
                <dd>{customer.phone ? customer.phone : <Muted>Not set</Muted>}</dd>
                <dt>Orders</dt>
                <dd>
                  {/*
                    A real customerId filter. The Java screen linked here by pasting
                    the customer's email into the order search box, which also matched
                    any order number that happened to contain it.
                  */}
                  {customer.orderCount ? (
                    <Link to={`/orders?customerId=${customer.id}`}>{customer.orderCount} order(s)</Link>
                  ) : (
                    <Muted>None yet</Muted>
                  )}
                </dd>
                <dt>Created</dt>
                <dd>{dateTime(customer.createdAt)}</dd>
              </dl>
            </Panel>

            <Panel title="Address">
              {!customer.hasAddress ? (
                <p className="muted">No address on file.</p>
              ) : (
                <dl className="detail">
                  <dt>Street</dt>
                  <dd>
                    {customer.address.line1 ?? '—'}
                    {customer.address.line2 ? `, ${customer.address.line2}` : ''}
                  </dd>
                  <dt>City</dt>
                  <dd>{customer.address.city ?? '—'}</dd>
                  <dt>State / region</dt>
                  <dd>{customer.address.state ?? '—'}</dd>
                  <dt>Postal code</dt>
                  <dd>{customer.address.postalCode ?? '—'}</dd>
                  <dt>Country</dt>
                  <dd>{customer.address.country ?? '—'}</dd>
                </dl>
              )}
            </Panel>
          </>
        )}
      </Loadable>
    </Layout>
  );
}
