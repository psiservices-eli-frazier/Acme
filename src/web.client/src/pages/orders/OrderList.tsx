import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ordersApi } from '../../api/resources';
import { AdminOnly } from '../../auth/AuthContext';
import { dateTime, money } from '../../format';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconPlus, IconTrash } from '../../components/Icons';
import { EmptyState, Layout, PageHead, PageTitle, Pill, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';
import { Pagination, SearchBox, SortHeader, useListState } from '../../components/ListControls';

const BASE = '/orders';

export function OrderList() {
  usePageTitle('Orders');

  const state = useListState();
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['orders', state],
    queryFn: () => ordersApi.list(state),
  });

  return (
    <Layout active="orders">
      <PageHead>
        <PageTitle badge={<IconBadge entity="order" />}>Orders</PageTitle>
        {/* Not gated: orders are the day-to-day work STAFF exists to do. */}
        <Link className="btn btn-primary" to="/orders/new">
          <IconPlus /> New order
        </Link>
      </PageHead>

      <SearchBox basePath={BASE} state={state} placeholder="Search by order number or customer" />

      {state.customerId && (
        <p className="muted" style={{ marginTop: -8, marginBottom: 16 }}>
          Filtered to one customer. <Link to={BASE}>Show all orders</Link>
        </p>
      )}

      <Loadable query={query}>
        {(page) =>
          page.totalElements === 0 ? (
            <EmptyState>
              {state.search || state.customerId
                ? 'No orders match that search.'
                : 'No orders yet. Create the first one.'}
            </EmptyState>
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    <SortHeader
                      property="orderNumber"
                      label="Order"
                      basePath={BASE}
                      state={state}
                      currentSort={page.sort}
                    />
                    <th>Customer</th>
                    <SortHeader property="status" label="Status" basePath={BASE} state={state} currentSort={page.sort} />
                    {/* Defaults to descending, so the toggle starts the other way round. */}
                    <SortHeader
                      property="orderedAt"
                      label="Ordered"
                      basePath={BASE}
                      state={state}
                      currentSort={page.sort}
                      defaultDirection="desc"
                    />
                    <th className="num">Total</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {page.content.map((order) => (
                    <tr key={order.id}>
                      <td>
                        <Link to={`/orders/${order.id}`}>{order.orderNumber}</Link>
                      </td>
                      <td>
                        {order.customer && (
                          <Link to={`/customers/${order.customer.id}`}>{order.customer.fullName}</Link>
                        )}
                      </td>
                      <td>
                        <Pill className={`status-${order.status}`}>{order.statusLabel}</Pill>
                      </td>
                      <td>{dateTime(order.orderedAt)}</td>
                      <td className="num">{money(order.total)}</td>
                      <td className="actions">
                        <Link className="btn btn-small" to={`/orders/${order.id}/edit`}>
                          <IconEdit /> Edit
                        </Link>{' '}
                        <AdminOnly>
                          <button
                            type="button"
                            className="btn btn-small btn-danger"
                            onClick={() =>
                              void remove({
                                confirm: 'Delete this order and all of its line items?',
                                run: () => ordersApi.remove(order.id),
                                success: 'Order was deleted.',
                                listPath: BASE,
                                invalidate: ['orders'],
                              })
                            }
                          >
                            <IconTrash /> Delete
                          </button>
                        </AdminOnly>
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
