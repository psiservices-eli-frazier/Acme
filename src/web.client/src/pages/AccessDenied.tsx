import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../components/Layout';

/**
 * Kept deliberately distinct from the not-found screen, so a refusal never implies
 * the record is missing.
 */
export function AccessDenied() {
  usePageTitle('Not permitted');
  const { user } = useAuth();

  return (
    <Layout>
      <PageHead>
        <PageTitle>Not permitted</PageTitle>
      </PageHead>
      <Panel>
        <p>Your account does not have permission to do that.</p>
        <p className="muted">
          Signed in as <strong>{user?.username}</strong>. An administrator can change what this account is
          allowed to do.
        </p>
        <div style={{ marginTop: 14 }}>
          <Link className="btn btn-primary" to="/">
            Back to dashboard
          </Link>
        </div>
      </Panel>
    </Layout>
  );
}
