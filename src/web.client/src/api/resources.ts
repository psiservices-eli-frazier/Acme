import { api, listQuery } from './client';
import type {
  CurrentUser,
  Customer,
  CustomerInput,
  CustomerOption,
  Dashboard,
  Order,
  OrderInput,
  OrderListItem,
  Paged,
  Product,
  ProductInput,
  ProductOption,
  StatusOption,
} from './types';

export interface ListParams {
  search?: string | null;
  /** Orders only: filter to one customer's orders. */
  customerId?: number | null;
  page?: number | null;
  size?: number | null;
  sort?: string | null;
}

export const productsApi = {
  list: (params: ListParams) => api.get<Paged<Product>>(`/api/products${listQuery(params)}`),
  get: (id: number) => api.get<Product>(`/api/products/${id}`),
  options: () => api.get<ProductOption[]>('/api/products/active'),
  create: (input: ProductInput) => api.post<Product>('/api/products', input),
  update: (id: number, input: ProductInput) => api.put<Product>(`/api/products/${id}`, input),
  remove: (id: number) => api.delete(`/api/products/${id}`),
};

export const customersApi = {
  list: (params: ListParams) => api.get<Paged<Customer>>(`/api/customers${listQuery(params)}`),
  get: (id: number) => api.get<Customer>(`/api/customers/${id}`),
  options: () => api.get<CustomerOption[]>('/api/customers/lookup'),
  create: (input: CustomerInput) => api.post<Customer>('/api/customers', input),
  update: (id: number, input: CustomerInput) => api.put<Customer>(`/api/customers/${id}`, input),
  remove: (id: number) => api.delete(`/api/customers/${id}`),
};

export const ordersApi = {
  list: (params: ListParams) => api.get<Paged<OrderListItem>>(`/api/orders${listQuery(params)}`),
  get: (id: number) => api.get<Order>(`/api/orders/${id}`),
  statuses: () => api.get<StatusOption[]>('/api/orders/statuses'),
  create: (input: OrderInput) => api.post<Order>('/api/orders', input),
  update: (id: number, input: OrderInput) => api.put<Order>(`/api/orders/${id}`, input),
  remove: (id: number) => api.delete(`/api/orders/${id}`),
};

export const dashboardApi = {
  get: () => api.get<Dashboard>('/api/dashboard'),
};

export const authApi = {
  me: () => api.get<CurrentUser>('/api/auth/me'),
  login: (username: string, password: string) =>
    api.post<CurrentUser>('/api/auth/login', { username, password }),
  logout: () => api.post<void>('/api/auth/logout', {}),
};
