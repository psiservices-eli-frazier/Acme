import { useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { usePageTitle } from '../components/Layout';

export function Login() {
  usePageTitle('Sign in');

  const { user, loading, signIn } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [failed, setFailed] = useState(false);
  const [busy, setBusy] = useState(false);

  if (loading) return null;
  if (user) return <Navigate to="/" replace />;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setFailed(false);
    try {
      await signIn(username, password);
      navigate('/', { replace: true });
    } catch (error) {
      // One message whichever half was wrong -- the server does not say which, and
      // neither should this.
      if (error instanceof ApiError && error.status === 401) setFailed(true);
      else setFailed(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    // The one screen with no top bar and no container: it centres its own card.
    <div className="auth-body">
      <div className="auth-card">
        <div className="auth-brand">
          Acme
        </div>
        <p className="auth-lede">Sign in to manage products, orders and customers.</p>

        {failed && <div className="alert alert-bad auth-alert">Incorrect username or password.</div>}
        {params.has('logout') && !failed && (
          <div className="alert alert-ok auth-alert">You have been signed out.</div>
        )}

        <form onSubmit={submit}>
          <div className="field">
            <label>
              Username
              <input
                type="text"
                name="username"
                autoComplete="username"
                autoFocus
                required
                value={username}
                onChange={(e) => setUsername(e.currentTarget.value)}
              />
            </label>
          </div>
          <div className="field">
            <label>
              Password
              <input
                type="password"
                name="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.currentTarget.value)}
              />
            </label>
          </div>
          <button type="submit" className="btn btn-primary auth-submit" disabled={busy}>
            Sign in
          </button>
        </form>

        {/*
          The Java version's hint text was stale: it advertised `admin` and `staff` as
          usernames, but those are the passwords -- the seeded usernames are adoyle,
          sokonkwo and jdoe.
        */}
        <p className="auth-hint">
          Demo accounts on this build: <code>adoyle</code> / <code>admin</code> for full access,{' '}
          <code>sokonkwo</code> / <code>staff</code> for read plus order entry.
        </p>
      </div>
    </div>
  );
}
