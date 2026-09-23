import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { productsApi } from '../../api/resources';
import { AdminOnly } from '../../auth/AuthContext';
import { dateTime, money } from '../../format';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconTrash } from '../../components/Icons';
import { Layout, Muted, PageHead, PageTitle, Panel, Pill, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';

export function ProductView() {
  const productId = Number(useParams().id);
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['product', productId],
    queryFn: () => productsApi.get(productId),
  });

  usePageTitle(query.data?.name ?? 'Product');

  return (
    <Layout active="products">
      <Loadable query={query}>
        {(product) => (
          <>
            <PageHead>
              <div>
                <p className="crumb">
                  <Link to="/products">Products</Link> / {product.sku}
                </p>
                <PageTitle badge={<IconBadge entity="product" />}>{product.name}</PageTitle>
              </div>
              <AdminOnly>
                <div style={{ display: 'flex', gap: 10 }}>
                  <Link className="btn" to={`/products/${product.id}/edit`}>
                    <IconEdit /> Edit
                  </Link>
                  <button
                    type="button"
                    className="btn btn-danger"
                    onClick={() =>
                      void remove({
                        confirm: 'Delete this product? This cannot be undone.',
                        run: () => productsApi.remove(product.id),
                        success: 'Product was deleted.',
                        listPath: '/products',
                        invalidate: ['products'],
                      })
                    }
                  >
                    <IconTrash /> Delete
                  </button>
                </div>
              </AdminOnly>
            </PageHead>

            <Panel title="Details">
              <dl className="detail">
                <dt>SKU</dt>
                <dd>{product.sku}</dd>
                <dt>Price</dt>
                <dd>{money(product.price)}</dd>
                <dt>Stock quantity</dt>
                <dd>{product.stockQuantity}</dd>
                <dt>Status</dt>
                <dd>
                  <Pill className={product.active ? 'pill-on' : 'pill-off'}>
                    {product.active ? 'Active' : 'Inactive'}
                  </Pill>
                </dd>
                <dt>Description</dt>
                <dd>{product.description ? product.description : <Muted>Not set</Muted>}</dd>
                <dt>Created</dt>
                <dd>{dateTime(product.createdAt)}</dd>
                <dt>Last updated</dt>
                <dd>{dateTime(product.updatedAt)}</dd>
              </dl>
            </Panel>
          </>
        )}
      </Loadable>
    </Layout>
  );
}
