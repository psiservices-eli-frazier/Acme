import { Link } from 'react-router-dom';
import { Layout, PageHead, PageTitle, Panel, usePageTitle } from '../components/Layout';

export function NotFound({ message }: { message?: string }) {
  usePageTitle('Not found');

  return (
    <Layout>
      <PageHead>
        <PageTitle>Not found</PageTitle>
      </PageHead>
      <Panel>
        <p>{message ?? 'That page does not exist.'}</p>
        <div style={{ marginTop: 14 }}>
          <Link className="btn" to="/">
            Back to dashboard
          </Link>
        </div>
      </Panel>
    </Layout>
  );
}
