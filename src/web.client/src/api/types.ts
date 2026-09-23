/** Wire types, mirroring the server's Contracts namespace. */

export type OrderStatus = 'NEW' | 'PAID' | 'SHIPPED' | 'DELIVERED' | 'CANCELLED';

export type Role = 'ADMIN' | 'STAFF';

/**
 * One page of results. Pages are zero-based and `sort` is `property,direction`,
 * matching the query string -- list state lives in the URL, not in component state.
 */
export interface Paged<T> {
  content: T[];
  page: number;
  size: number;
  totalElements: number;
  totalPages: number;
  first: boolean;
  last: boolean;
  search: string | null;
  sort: string;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  description: string | null;
  price: number;
  stockQuantity: number;
  active: boolean;
  displayName: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface Address {
  line1: string | null;
  line2: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  country: string | null;
}

export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phone: string | null;
  displayName: string;
  hasAddress: boolean;
  address: Address;
  createdAt: string;
  updatedAt: string | null;
  /** Only present on the single-customer read. */
  orderCount: number | null;
}

export interface OrderCustomer {
  id: number;
  fullName: string;
  email: string;
}

export interface OrderListItem {
  id: number;
  orderNumber: string;
  customer: OrderCustomer | null;
  status: OrderStatus;
  statusLabel: string;
  orderedAt: string;
  total: number;
}

export interface OrderLine {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: number;
  orderNumber: string;
  customer: OrderCustomer | null;
  status: OrderStatus;
  statusLabel: string;
  orderedAt: string;
  notes: string | null;
  items: OrderLine[];
  total: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductOption {
  id: number;
  displayName: string;
  price: number;
}

export interface CustomerOption {
  id: number;
  displayName: string;
}

export interface StatusOption {
  value: OrderStatus;
  label: string;
}

export interface Dashboard {
  productCount: number;
  customerCount: number;
  orderCount: number;
}

export interface CurrentUser {
  username: string;
  displayName: string;
  roles: Role[];
}

// ---- request payloads -------------------------------------------------------
//
// None of these carry an id: the thing being written is named by the route. The
// order payload carries no order number and no unit prices either -- both are
// decided server-side.

export interface ProductInput {
  sku: string;
  name: string;
  description: string | null;
  price: number | null;
  stockQuantity: number | null;
  active: boolean;
}

export interface CustomerInput {
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  address: Address;
}

export interface OrderLineInput {
  productId: number | null;
  quantity: number | null;
}

export interface OrderInput {
  customerId: number | null;
  status: OrderStatus;
  notes: string | null;
  items: OrderLineInput[];
}
