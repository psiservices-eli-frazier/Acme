/**
 * The HTTP layer.
 *
 * Two things here are less obvious than they look:
 *
 *  1. The antiforgery token is bound to the caller's identity, so a token obtained
 *     while signed out stops validating the moment you sign in. The cache is cleared
 *     explicitly around sign-in and sign-out, and any 403 that names the token is
 *     retried once with a fresh one.
 *
 *  2. Validation errors come back keyed by the server's property name in PascalCase
 *     ("Sku"), while the JSON bodies are camelCase ("sku"). `fieldError` looks up
 *     case-insensitively so a form can ask for the field it rendered.
 */

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly errors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** The first validation message for a field, whatever case the server used. */
  fieldError(field: string): string | undefined {
    if (!this.errors) return undefined;
    const key = Object.keys(this.errors).find((k) => k.toLowerCase() === field.toLowerCase());
    return key ? this.errors[key][0] : undefined;
  }

  get isValidation(): boolean {
    return this.status === 400 && !!this.errors;
  }
}

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

let cachedToken: string | null = null;

export function invalidateCsrfToken(): void {
  cachedToken = null;
}

async function csrfToken(): Promise<string> {
  if (cachedToken === null) {
    const response = await fetch('/api/antiforgery/token', { credentials: 'same-origin' });
    if (!response.ok) throw new ApiError(response.status, 'Could not obtain an antiforgery token.');
    cachedToken = ((await response.json()) as { token: string }).token;
  }
  return cachedToken;
}

async function toError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    /* a body is not guaranteed, e.g. on 401 */
  }

  const message =
    problem.detail ??
    problem.title ??
    (response.status === 401 ? 'You are not signed in.' : 'Something went wrong.');

  return new ApiError(response.status, message, problem.errors);
}

async function send(method: string, path: string, body?: unknown, retry = true): Promise<Response> {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (!SAFE_METHODS.has(method)) headers['X-CSRF-TOKEN'] = await csrfToken();

  const response = await fetch(path, {
    method,
    headers,
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  // The signed-in identity changed under us; get a token for the new one and retry.
  if (response.status === 403 && retry && !SAFE_METHODS.has(method)) {
    const clone = response.clone();
    const problem = (await clone.json().catch(() => ({}))) as ProblemDetails;
    if (problem.detail?.toLowerCase().includes('antiforgery')) {
      invalidateCsrfToken();
      return send(method, path, body, false);
    }
  }

  return response;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await send(method, path, body);
  if (!response.ok) throw await toError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T,>(path: string) => request<T>('GET', path),
  post: <T,>(path: string, body: unknown) => request<T>('POST', path, body),
  put: <T,>(path: string, body: unknown) => request<T>('PUT', path, body),
  delete: (path: string) => request<void>('DELETE', path),
};

/**
 * Builds a list query string. Empty values are omitted so the URL stays as short as
 * the Thymeleaf links were, and `page` is dropped when it is the first page.
 */
export function listQuery(params: {
  search?: string | null;
  customerId?: number | null;
  page?: number | null;
  size?: number | null;
  sort?: string | null;
}): string {
  const query = new URLSearchParams();
  if (params.search) query.set('search', params.search);
  if (params.customerId) query.set('customerId', String(params.customerId));
  if (params.page) query.set('page', String(params.page));
  if (params.size) query.set('size', String(params.size));
  if (params.sort) query.set('sort', params.sort);
  const text = query.toString();
  return text ? `?${text}` : '';
}
