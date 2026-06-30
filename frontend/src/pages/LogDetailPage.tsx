import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { MetricCard } from '../components/MetricCard';
import { EmptyState } from '../components/EmptyState';
import { mockFlows, mockActions } from '../data/mockData';

function formatDuration(ms: number): string {
  if (ms >= 1000) {
    return `${(ms / 1000).toFixed(1)}s`;
  }
  return `${ms}ms`;
}

function formatFieldValue(value: string | undefined | null): string {
  return value ?? '—';
}

export function LogDetailPage() {
  const { flowId } = useParams<{ flowId: string }>();
  const flow = mockFlows.find((f) => f.flowId === flowId);
  const flowActions = mockActions.filter((a) => a.flowId === flowId);

  if (!flow) {
    return (
      <div className="p-6">
        <Link
          to="/logs"
          className="inline-flex items-center text-sm text-gray-600 hover:text-gray-900 mb-4"
        >
          <svg className="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Logs
        </Link>
        <EmptyState message="Flow not found" />
      </div>
    );
  }

  return (
    <div className="p-6">
      <Link
        to="/logs"
        className="inline-flex items-center text-sm text-gray-600 hover:text-gray-900 mb-4"
      >
        <svg className="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Logs
      </Link>

      <PageHeader
        title="Flow Detail"
        subtitle="Trace actions and inspect technical details for this flow"
      >
        <div className="mt-2">
          <StatusBadge status={flow.status} />
        </div>
      </PageHeader>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm mb-6">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Flow Summary</h2>
        </div>
        <div className="p-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <div>
              <p className="text-sm text-gray-500">Flow ID</p>
              <p className="text-sm font-medium text-gray-900">{flow.flowId}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Flow Type</p>
              <p className="text-sm font-medium text-gray-900">{flow.flowType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Checkout Type</p>
              <p className="text-sm font-medium text-gray-900">{flow.checkoutType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Status</p>
              <StatusBadge status={flow.status} />
            </div>
            <div>
              <p className="text-sm text-gray-500">Last Action</p>
              <p className="text-sm font-medium text-gray-900">{flow.lastActionType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Last Message</p>
              <p className="text-sm font-medium text-gray-900">{flow.lastMessage}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Started At</p>
              <p className="text-sm font-medium text-gray-900">
                {new Date(flow.startedAt).toLocaleString()}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Completed At</p>
              <p className="text-sm font-medium text-gray-900">
                {formatFieldValue(flow.completedAt ? new Date(flow.completedAt).toLocaleString() : undefined)}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Updated At</p>
              <p className="text-sm font-medium text-gray-900">
                {new Date(flow.updatedAt).toLocaleString()}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Order ID</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.orderId)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Customer Email</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.customerEmail)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Customer Phone</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.customerPhone)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Payment ID</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.paymentId)}</p>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <MetricCard title="Total Steps" value={flow.totalSteps} />
        <MetricCard title="Success Steps" value={flow.successSteps} />
        <MetricCard title="Failed Steps" value={flow.failedSteps} />
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm mb-6">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Action Timeline</h2>
        </div>
        <div className="p-6">
          {flowActions.length === 0 ? (
            <p className="text-sm text-gray-500">No actions found for this flow.</p>
          ) : (
            <div className="space-y-4">
              {flowActions.map((action, index) => (
                <div key={action.actionId} className="flex gap-4">
                  <div className="flex flex-col items-center">
                    <div className="w-3 h-3 rounded-full bg-blue-600"></div>
                    {index < flowActions.length - 1 && (
                      <div className="w-0.5 h-full bg-gray-200 mt-1"></div>
                    )}
                  </div>
                  <div className="flex-1 pb-4">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-sm font-medium text-gray-900">{action.actionType}</span>
                      <StatusBadge status={action.status} />
                    </div>
                    <p className="text-sm text-gray-600 mb-1">{action.message}</p>
                    <div className="flex items-center gap-4 text-xs text-gray-500">
                      <span>{action.serviceName}</span>
                      <span>•</span>
                      <span>{formatDuration(action.durationMs)}</span>
                      <span>•</span>
                      <span>{new Date(action.createdAt).toLocaleString()}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Technical Details</h2>
        </div>
        <div className="p-6">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div>
              <h3 className="text-sm font-medium text-gray-700 mb-2">Request</h3>
              <pre className="bg-gray-50 p-4 rounded-lg text-xs text-gray-600 overflow-x-auto">
{`{
  "flowId": "${flow.flowId}",
  "flowType": "${flow.flowType}",
  "customerEmail": "${flow.customerEmail}",
  "customerPhone": "${flow.customerPhone}",
  "orderId": "${flow.orderId}",
  "paymentId": "${flow.paymentId}"
}`}</pre>
            </div>
            <div>
              <h3 className="text-sm font-medium text-gray-700 mb-2">Response</h3>
              <pre className="bg-gray-50 p-4 rounded-lg text-xs text-gray-600 overflow-x-auto">
{`{
  "status": "${flow.status}",
  "successSteps": ${flow.successSteps},
  "totalSteps": ${flow.totalSteps},
  "lastMessage": "${flow.lastMessage}"
}`}</pre>
            </div>
            <div>
              <h3 className="text-sm font-medium text-gray-700 mb-2">Error</h3>
              <pre className="bg-gray-50 p-4 rounded-lg text-xs text-gray-600 overflow-x-auto">
{`{
  "code": ${flow.failedSteps > 0 ? '"PAYMENT_DECLINED"' : 'null'},
  "message": ${flow.failedSteps > 0 ? '"Payment declined: insufficient funds"' : 'null'},
  "timestamp": "${flow.completedAt ?? flow.updatedAt}"
}`}</pre>
            </div>
          </div>
          <p className="text-xs text-gray-500 mt-4">Sensitive fields are masked.</p>
        </div>
      </div>
    </div>
  );
}
