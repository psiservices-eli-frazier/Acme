import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ordersApi } from '../../api/resources';
import { AdminOnly } from '../../auth/AuthContext';
import { dateTime, money } from '../../format';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconTrash } from '../../components/Icons';
import { Layout, Muted, PageHead, PageTitle, Panel, Pill, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';

export function OrderView() {
  const orderId = Number(useParams().id);
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['order', orderId],
    queryFn: () => ordersApi.get(orderId),
  });

  usePageTitle(query.data?.orderNumber ?? 'Order');

  return (
    <Layout active="orders">
      <Loadable query={query}>
        {(order) => (
          <>
            <PageHead>
              <div>
                <p className="crumb">
                  <Link to="/orders">Orders</Link> / {order.orderNumber}
                </p>
                <PageTitle badge={<IconBadge entity="order" />}>{order.orderNumber}</PageTitle>
              </div>
              <div style={{ display: 'flex', gap: 10 }}>
                <Link className="btn" to={`/orders/${order.id}/edit`}>
                  <IconEdit /> Edit
                </Link>
                <AdminOnly>
                  <button
                    type="button"
                    className="btn btn-danger"
                    onClick={() =>
                      void remove({
                        confirm: 'Delete this order and all of its line items?',
                        run: () => ordersApi.remove(order.id),
                        success: 'Order was deleted.',
                        listPath: '/orders',
                        invalidate: ['orders'],
                      })
                    }
                  >
                    <IconTrash /> Delete
                  </button>
                </AdminOnly>
              </div>
            </PageHead>

            <Panel title="Summary">
              <dl className="detail">
                <dt>Customer</dt>
                <dd>
                  {order.customer && (
                    <>
                      <Link to={`/customers/${order.customer.id}`}>{order.customer.fullName}</Link>{' '}
                      <Muted>· {order.customer.email}</Muted>
                    </>
                  )}
                </dd>
                <dt>Status</dt>
                <dd>
                  <Pill className={`status-${order.status}`}>{order.statusLabel}</Pill>
                </dd>
                <dt>Ordered</dt>
                <dd>{dateTime(order.orderedAt)}</dd>
                <dt>Notes</dt>
                <dd>{order.notes ? order.notes : <Muted>None</Muted>}</dd>
              </dl>
            </Panel>

            <Panel title="Line items">
              <table>
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>SKU</th>
                    <th className="num">Unit price</th>
                    <th className="num">Qty</th>
                    <th className="num">Line total</th>
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((line) => (
                    <tr key={line.id}>
                      <td>
                        <Link to={`/products/${line.productId}`}>{line.productName}</Link>
                      </td>
                      <td>{line.productSku}</td>
                      <td className="num">{money(line.unitPrice)}</td>
                      <td className="num">{line.quantity}</td>
                      <td className="num">{money(line.lineTotal)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="total-row">
                    <td colSpan={4}>Order total</td>
                    <td className="num">{money(order.total)}</td>
                  </tr>
                </tfoot>
              </table>
              <p className="hint" style={{ marginTop: 10 }}>
                Unit prices are captured when a line is created, so repricing a product does not alter
                existing orders.
              </p>
            </Panel>
          </>
        )}
      </Loadable>
    </Layout>
  );
}
