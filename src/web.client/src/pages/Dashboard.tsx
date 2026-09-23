import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { dashboardApi } from '../api/resources';
import { AdminOnly } from '../auth/AuthContext';
import { IconBadge, IconPlus } from '../components/Icons';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../components/Layout';
import { Loadable } from '../components/Loadable';

export function Dashboard() {
  usePageTitle('Dashboard');
  const query = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.get });

  return (
    <Layout>
      <PageHead>
        <PageTitle>Dashboard</PageTitle>
      </PageHead>

      <Loadable query={query}>
        {(counts) => (
          <div className="tiles">
            <Link className="tile" to="/products">
              <div className="tile-icon">
                <IconBadge entity="product" />
              </div>
              <div className="tile-label">Products</div>
              <div className="tile-value">{counts.productCount}</div>
            </Link>
            <Link className="tile" to="/orders">
              <div className="tile-icon">
                <IconBadge entity="order" />
              </div>
              <div className="tile-label">Orders</div>
              <div className="tile-value">{counts.orderCount}</div>
            </Link>
            <Link className="tile" to="/customers">
              <div className="tile-icon">
                <IconBadge entity="customer" />
              </div>
              <div className="tile-label">Customers</div>
              <div className="tile-value">{counts.customerCount}</div>
            </Link>
          </div>
        )}
      </Loadable>

      <Panel title="Getting started">
        <p className="muted">
          Business requirements are still being defined, so this is a deliberately plain CRUD skeleton over
          the three core entities. Each area supports list, search, create, view, edit and delete.
        </p>
        <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
          {/* Orders are the day-to-day work, so this one is not admin-gated. */}
          <Link className="btn btn-primary" to="/orders/new">
            <IconPlus /> New order
          </Link>
          <AdminOnly>
            <Link className="btn" to="/products/new">
              <IconPlus /> New product
            </Link>
          </AdminOnly>
          <AdminOnly>
            <Link className="btn" to="/customers/new">
              <IconPlus /> New customer
            </Link>
          </AdminOnly>
        </div>
      </Panel>
    </Layout>
  );
}
