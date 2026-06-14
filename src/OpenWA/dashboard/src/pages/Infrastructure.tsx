import { Database, HardDrive, Server, Webhook, CheckCircle, XCircle, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '../hooks/useDocumentTitle';
import { useInfraStatusQuery } from '../hooks/queries';
import { PageHeader } from '../components/PageHeader';
import './Infrastructure.css';

function StatusPill({ connected }: { connected: boolean }) {
  return (
    <span className={`status-indicator ${connected ? 'connected' : 'disconnected'}`}>
      {connected ? <CheckCircle size={14} /> : <XCircle size={14} />}
      {connected ? 'Connected' : 'Unavailable'}
    </span>
  );
}

export function Infrastructure() {
  const { t } = useTranslation();
  useDocumentTitle(t('infrastructure.title'));
  const { data: infraStatus, isLoading: loading } = useInfraStatusQuery();

  if (loading || !infraStatus) {
    return (
      <div
        className="infrastructure-page"
        style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '400px' }}
      >
        <Loader2 className="animate-spin" size={32} />
      </div>
    );
  }

  return (
    <div className="infrastructure-page">
      <PageHeader title={t('infrastructure.title')} subtitle="Runtime resources are configured by the Aspire AppHost." />

      <div className="infra-sections">
        <section className="infra-card">
          <div className="card-header">
            <div className="header-left">
              <Database size={20} />
              <h2>PostgreSQL</h2>
            </div>
            <StatusPill connected={infraStatus.database.connected} />
          </div>
          <dl className="status-grid">
            <div>
              <dt>Type</dt>
              <dd>{infraStatus.database.type}</dd>
            </div>
            <div>
              <dt>Host</dt>
              <dd>{infraStatus.database.host}</dd>
            </div>
          </dl>
        </section>

        <section className="infra-card">
          <div className="card-header">
            <div className="header-left">
              <Server size={20} />
              <h2>Redis</h2>
            </div>
            <StatusPill connected={infraStatus.redis.connected} />
          </div>
          <dl className="status-grid">
            <div>
              <dt>Enabled</dt>
              <dd>{infraStatus.redis.enabled ? 'Yes' : 'No'}</dd>
            </div>
            <div>
              <dt>Endpoint</dt>
              <dd>
                {infraStatus.redis.host}:{infraStatus.redis.port}
              </dd>
            </div>
          </dl>
        </section>

        <section className="infra-card">
          <div className="card-header">
            <div className="header-left">
              <Webhook size={20} />
              <h2>Webhook Queue</h2>
            </div>
            <span className={`status-indicator ${infraStatus.queue.enabled ? 'connected' : 'disconnected'}`}>
              {infraStatus.queue.enabled ? 'Enabled' : 'Disabled'}
            </span>
          </div>
          <dl className="status-grid">
            <div>
              <dt>Pending</dt>
              <dd>{infraStatus.queue.webhooks.pending}</dd>
            </div>
            <div>
              <dt>Completed</dt>
              <dd>{infraStatus.queue.webhooks.completed}</dd>
            </div>
            <div>
              <dt>Failed</dt>
              <dd>{infraStatus.queue.webhooks.failed}</dd>
            </div>
          </dl>
        </section>

        <section className="infra-card">
          <div className="card-header">
            <div className="header-left">
              <HardDrive size={20} />
              <h2>Storage</h2>
            </div>
            <span className="status-indicator connected">{infraStatus.storage.type}</span>
          </div>
          <dl className="status-grid">
            <div>
              <dt>Path</dt>
              <dd>{infraStatus.storage.path || infraStatus.storage.bucket || '-'}</dd>
            </div>
            <div>
              <dt>Session Data</dt>
              <dd>{infraStatus.engine.sessionDataPath}</dd>
            </div>
          </dl>
        </section>
      </div>
    </div>
  );
}
