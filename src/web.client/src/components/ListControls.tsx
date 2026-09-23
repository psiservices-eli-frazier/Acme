import { useSearchParams } from 'react-router-dom';
import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { Paged } from '../api/types';
import { IconSearch } from './Icons';

/**
 * List state read straight out of the query string.
 *
 * It lives in the URL rather than in component state, which is the property Acme
 * had and is worth keeping: a filtered, sorted page is a link you can share, bookmark
 * and reload.
 */
export interface ListState {
  search: string;
  /** Orders only: the customer detail screen links here to filter by customer. */
  customerId: number | null;
  page: number;
  size: number;
  sort: string | null;
}

export function useListState(): ListState {
  const [params] = useSearchParams();
  const asNumber = (value: string | null, fallback: number) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
  };

  const customerId = params.get('customerId');

  return {
    search: params.get('search') ?? '',
    customerId: customerId ? asNumber(customerId, 0) || null : null,
    page: asNumber(params.get('page'), 0),
    size: asNumber(params.get('size'), 10),
    sort: params.get('sort'),
  };
}

function href(basePath: string, params: Record<string, string | number | null | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== '') query.set(key, String(value));
  }
  const text = query.toString();
  return text ? `${basePath}?${text}` : basePath;
}

/**
 * A sortable column header.
 *
 * The link carries the current search and page size forward but deliberately omits
 * `page`, so changing the sort returns you to the first page. The direction toggle
 * compares against the sort the server resolved, not the raw query string, so the
 * first click on a column that is already the default flips it.
 */
export function SortHeader({
  property,
  label,
  basePath,
  state,
  currentSort,
  defaultDirection = 'asc',
  className,
}: {
  property: string;
  label: string;
  basePath: string;
  state: ListState;
  currentSort: string;
  defaultDirection?: 'asc' | 'desc';
  className?: string;
}) {
  const other = defaultDirection === 'asc' ? 'desc' : 'asc';
  const next =
    currentSort === `${property},${defaultDirection}`
      ? `${property},${other}`
      : `${property},${defaultDirection}`;

  return (
    <th className={className}>
      <Link
        to={href(basePath, {
          search: state.search,
          customerId: state.customerId,
          size: state.size === 10 ? null : state.size,
          sort: next,
        })}
      >
        {label}
      </Link>
    </th>
  );
}

export function SearchBox({
  basePath,
  state,
  placeholder,
}: {
  basePath: string;
  state: ListState;
  placeholder: string;
}) {
  const [, setParams] = useSearchParams();

  return (
    <form
      className="search"
      onSubmit={(event) => {
        event.preventDefault();
        const value = new FormData(event.currentTarget).get('search');
        const text = typeof value === 'string' ? value.trim() : '';
        // A new search resets page, size and sort, exactly like the plain GET form it
        // replaces. An active customer filter is not a search term, so it survives.
        const next: Record<string, string> = {};
        if (text) next.search = text;
        if (state.customerId) next.customerId = String(state.customerId);
        setParams(next);
      }}
    >
      <input type="search" name="search" defaultValue={state.search} placeholder={placeholder} key={state.search} />
      <button type="submit" className="btn">
        <IconSearch /> Search
      </button>
      {state.search && (
        <Link className="btn btn-quiet" to={basePath}>
          Clear
        </Link>
      )}
    </form>
  );
}

/** Hidden entirely when there is only one page, as the Thymeleaf fragment was. */
export function Pagination<T>({
  page,
  basePath,
  state,
}: {
  page: Paged<T>;
  basePath: string;
  state: ListState;
}) {
  if (page.totalPages <= 1) return null;

  const link = (target: number) =>
    href(basePath, {
      page: target === 0 ? null : target,
      size: page.size === 10 ? null : page.size,
      search: state.search,
      customerId: state.customerId,
      sort: page.sort,
    });

  return (
    <nav className="pager">
      {!page.first && (
        <Link className="btn btn-small" to={link(page.page - 1)}>
          &larr; Previous
        </Link>
      )}
      <span className="pager-state">
        {/* Displayed one-based over a zero-based parameter, as before. */}
        Page {page.page + 1} of {page.totalPages} ({page.totalElements} total)
      </span>
      {!page.last && (
        <Link className="btn btn-small" to={link(page.page + 1)}>
          Next &rarr;
        </Link>
      )}
    </nav>
  );
}

export function TableActions({ children }: { children: ReactNode }) {
  return <td className="actions">{children}</td>;
}
