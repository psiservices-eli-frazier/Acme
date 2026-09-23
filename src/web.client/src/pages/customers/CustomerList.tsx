import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { customersApi } from '../../api/resources';
import type { Customer } from '../../api/types';
import { AdminOnly, useAuth } from '../../auth/AuthContext';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconPlus, IconTrash } from '../../components/Icons';
import { EmptyState, Layout, Muted, PageHead, PageTitle, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';
import { Pagination, SearchBox, SortHeader, useListState } from '../../components/ListControls';

const BASE = '/customers';

function location(customer: Customer) {
  const { city, state } = customer.address;
  if (!city) return <Muted>—</Muted>;
  return <>{state ? `${city}, ${state}` : city}</>;
}

export function CustomerList() {
  usePageTitle('Customers');

  const state = useListState();
  const { isAdmin } = useAuth();
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['customers', state],
    queryFn: () => customersApi.list(state),
  });

  return (
    <Layout active="customers">
      <PageHead>
        <PageTitle badge={<IconBadge entity="customer" />}>Customers</PageTitle>
        <AdminOnly>
          <Link className="btn btn-primary" to="/customers/new">
            <IconPlus /> New customer
          </Link>
        </AdminOnly>
      </PageHead>

      <SearchBox basePath={BASE} state={state} placeholder="Search by name or email" />

      <Loadable query={query}>
        {(page) =>
          page.totalElements === 0 ? (
            <EmptyState>
              {state.search ? 'No customers match that search.' : 'No customers yet. Create the first one.'}
            </EmptyState>
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    {/* The Name column sorts on last name alone, as it did in Java. */}
                    <SortHeader
                      property="lastName"
                      label="Name"
                      basePath={BASE}
                      state={state}
                      currentSort={page.sort}
                    />
                    <SortHeader property="email" label="Email" basePath={BASE} state={state} currentSort={page.sort} />
                    <th>Phone</th>
                    <th>Location</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {page.content.map((customer) => (
                    <tr key={customer.id}>
                      <td>
                        <Link to={`/customers/${customer.id}`}>{customer.fullName}</Link>
                      </td>
                      <td>{customer.email}</td>
                      <td>{customer.phone ? customer.phone : <Muted>—</Muted>}</td>
                      <td>{location(customer)}</td>
                      <td className="actions">
                        {isAdmin && (
                          <>
                            <Link className="btn btn-small" to={`/customers/${customer.id}/edit`}>
                              <IconEdit /> Edit
                            </Link>{' '}
                            <button
                              type="button"
                              className="btn btn-small btn-danger"
                              onClick={() =>
                                void remove({
                                  confirm: 'Delete this customer? This cannot be undone.',
                                  run: () => customersApi.remove(customer.id),
                                  success: 'Customer was deleted.',
                                  listPath: BASE,
                                  invalidate: ['customers'],
                                })
                              }
                            >
                              <IconTrash /> Delete
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <Pagination page={page} basePath={BASE} state={state} />
            </>
          )
        }
      </Loadable>
    </Layout>
  );
}
