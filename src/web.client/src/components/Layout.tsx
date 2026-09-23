import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Alerts } from './Flash';
import { IconCustomer, IconOrder, IconProduct } from './Icons';

/** Keeps the `Acme · <page>` title convention from the Thymeleaf head fragment. */
export function usePageTitle(title: string): void {
  useEffect(() => {
    document.title = `Acme · ${title}`;
  }, [title]);
}

export type NavSection = 'products' | 'orders' | 'customers' | null;

function TopBar({ active }: { active: NavSection }) {
  const { user, isAdmin, signOut } = useAuth();

  return (
    <header className="topbar">
      <div className="container topbar-inner">
        <Link className="brand" to="/">
          Acme
        </Link>

        {/*
          Nav icons are plain rather than the tinted badge versions used elsewhere: a
          pastel badge would wash out against the dark bar, so they inherit the link's
          own colour the way the label already does.
        */}
        <nav className="mainnav">
          <NavLink to="/products" className={active === 'products' ? 'on' : undefined}>
            <IconProduct /> Products
          </NavLink>
          <NavLink to="/orders" className={active === 'orders' ? 'on' : undefined}>
            <IconOrder /> Orders
          </NavLink>
          <NavLink to="/customers" className={active === 'customers' ? 'on' : undefined}>
            <IconCustomer /> Customers
          </NavLink>
        </nav>

        {user && (
          <div className="topbar-user">
            <span className="who">
              <span className="who-name">{user.username}</span>
              {/* Informational. It is not what enforces anything. */}
              <span className="who-role">{isAdmin ? 'Admin' : 'Staff'}</span>
            </span>
            <button type="button" className="btn btn-small btn-logout" onClick={() => void signOut()}>
              Sign out
            </button>
          </div>
        )}
      </div>
    </header>
  );
}

function Footer() {
  return (
    <footer className="footer">
      <div className="container">Acme · ASP.NET Core 10 · React 19</div>
    </footer>
  );
}

export function Layout({ active = null, children }: { active?: NavSection; children: ReactNode }) {
  return (
    <>
      <TopBar active={active} />
      <Alerts />
      <main className="container">{children}</main>
      <Footer />
    </>
  );
}

export function PageHead({ children }: { children: ReactNode }) {
  return <div className="page-head">{children}</div>;
}

export function PageTitle({ badge, children }: { badge?: ReactNode; children: ReactNode }) {
  return (
    <div className="page-title">
      {badge}
      <h1>{children}</h1>
    </div>
  );
}

export function Panel({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <div className="panel">
      {title && <h2>{title}</h2>}
      {children}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <p className="empty">{children}</p>;
}

export function Pill({ className, children }: { className?: string; children: ReactNode }) {
  return <span className={className ? `pill ${className}` : 'pill'}>{children}</span>;
}

export function Muted({ children }: { children: ReactNode }) {
  return <span className="muted">{children}</span>;
}
