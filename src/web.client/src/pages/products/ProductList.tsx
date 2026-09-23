import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { productsApi } from '../../api/resources';
import { AdminOnly, useAuth } from '../../auth/AuthContext';
import { money } from '../../format';
import { useDeleteAction } from '../../hooks/useDeleteAction';
import { IconBadge, IconEdit, IconPlus, IconTrash } from '../../components/Icons';
import { EmptyState, Layout, PageHead, PageTitle, Pill, usePageTitle } from '../../components/Layout';
import { Loadable } from '../../components/Loadable';
import { Pagination, SearchBox, SortHeader, useListState } from '../../components/ListControls';

const BASE = '/products';

export function ProductList() {
  usePageTitle('Products');

  const state = useListState();
  const { isAdmin } = useAuth();
  const remove = useDeleteAction();

  const query = useQuery({
    queryKey: ['products', state],
    queryFn: () => productsApi.list(state),
  });

  return (
    <Layout active="products">
      <PageHead>
        <PageTitle badge={<IconBadge entity="product" />}>Products</PageTitle>
        <AdminOnly>
          <Link className="btn btn-primary" to="/products/new">
            <IconPlus /> New product
          </Link>
        </AdminOnly>
      </PageHead>

      <SearchBox basePath={BASE} state={state} placeholder="Search by name or SKU" />

      <Loadable query={query}>
        {(page) =>
          page.totalElements === 0 ? (
            <EmptyState>
              {state.search ? 'No products match that search.' : 'No products yet. Create the first one.'}
            </EmptyState>
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    <SortHeader property="sku" label="SKU" basePath={BASE} state={state} currentSort={page.sort} />
                    <SortHeader property="name" label="Name" basePath={BASE} state={state} currentSort={page.sort} />
                    <SortHeader
                      property="price"
                      label="Price"
                      basePath={BASE}
                      state={state}
                      currentSort={page.sort}
                      className="num"
                    />
                    <SortHeader
                      property="stockQuantity"
                      label="Stock"
                      basePath={BASE}
                      state={state}
                      currentSort={page.sort}
                      className="num"
                    />
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {page.content.map((product) => (
                    <tr key={product.id}>
                      <td>
                        <Link to={`/products/${product.id}`}>{product.sku}</Link>
                      </td>
                      <td>{product.name}</td>
                      <td className="num">{money(product.price)}</td>
                      <td className="num">{product.stockQuantity}</td>
                      <td>
                        <Pill className={product.active ? 'pill-on' : 'pill-off'}>
                          {product.active ? 'Active' : 'Inactive'}
                        </Pill>
                      </td>
                      {/* An empty cell for non-admins keeps the row's column count. */}
                      <td className="actions">
                        {isAdmin && (
                          <>
                            <Link className="btn btn-small" to={`/products/${product.id}/edit`}>
                              <IconEdit /> Edit
                            </Link>{' '}
                            <button
                              type="button"
                              className="btn btn-small btn-danger"
                              onClick={() =>
                                void remove({
                                  confirm: 'Delete this product? This cannot be undone.',
                                  run: () => productsApi.remove(product.id),
                                  success: 'Product was deleted.',
                                  listPath: BASE,
                                  invalidate: ['products'],
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
