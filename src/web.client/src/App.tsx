import { Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { AccessDenied } from './pages/AccessDenied';
import { Dashboard } from './pages/Dashboard';
import { Login } from './pages/Login';
import { NotFound } from './pages/NotFound';
import { CustomerForm } from './pages/customers/CustomerForm';
import { CustomerList } from './pages/customers/CustomerList';
import { CustomerView } from './pages/customers/CustomerView';
import { OrderForm } from './pages/orders/OrderForm';
import { OrderList } from './pages/orders/OrderList';
import { OrderView } from './pages/orders/OrderView';
import { ProductForm } from './pages/products/ProductForm';
import { ProductList } from './pages/products/ProductList';
import { ProductView } from './pages/products/ProductView';

/**
 * Every screen is behind a sign-in, as in BrandX -- there is no signed-out view of
 * anything except the login page itself.
 */
function RequireAuth() {
  const { user, loading } = useAuth();
  if (loading) return null;
  return user ? <Outlet /> : <Navigate to="/login" replace />;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route element={<RequireAuth />}>
        <Route path="/" element={<Dashboard />} />

        <Route path="/products" element={<ProductList />} />
        <Route path="/products/new" element={<ProductForm />} />
        <Route path="/products/:id" element={<ProductView />} />
        <Route path="/products/:id/edit" element={<ProductForm />} />

        <Route path="/customers" element={<CustomerList />} />
        <Route path="/customers/new" element={<CustomerForm />} />
        <Route path="/customers/:id" element={<CustomerView />} />
        <Route path="/customers/:id/edit" element={<CustomerForm />} />

        <Route path="/orders" element={<OrderList />} />
        <Route path="/orders/new" element={<OrderForm />} />
        <Route path="/orders/:id" element={<OrderView />} />
        <Route path="/orders/:id/edit" element={<OrderForm />} />

        <Route path="/access-denied" element={<AccessDenied />} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}
