import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { MetricCard } from '../components/MetricCard';
import { StatusBadge } from '../components/StatusBadge';
import { getLogFlows, getDashboardMetrics } from '../services/logFlowService';
import type {
  LogFlowSummary,
  DashboardMetrics,
} from '../types/logFlow';

const RECENT_PAGE_SIZE = 10;
const EMPTY = '—';
const MAX_USER_LENGTH = 28;

function formatDuration(ms: number | null | undefined): string {
  if (ms == null) return EMPTY;
  const value = ms;

  if (value < 1000) {
    return `${Math.round(value)}ms`;
  }

  if (value < 60_000) {
    return `${(value / 1000).toFixed(1)}s`;
  }

  if (value < 3_600_000) {
    const totalSeconds = Math.floor(value / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return seconds > 0 ? `${minutes}m ${seconds}s` : `${minutes}m`;
  }

  const totalSeconds = Math.floor(value / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
}

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const d = new Date(value);
  if (isNaN(d.getTime())) return EMPTY;
  return d.toLocaleString();
}

function resolveDisplayUser(flow: LogFlowSummary): {
  value: string;
  truncated: boolean;
  full: string;
} {
  const email = flow.customerEmail?.trim();
  if (email) {
    return { value: email, truncated: false, full: email };
  }

  const userEmail = (flow as LogFlowSummary & { userEmail?: string | null }).userEmail?.trim();
  if (userEmail) {
    return { value: userEmail, truncated: false, full: userEmail };
  }

  const userId = flow.userId?.trim();
  if (userId) {
    if (userId.length <= MAX_USER_LENGTH) {
      return { value: userId, truncated: false, full: userId };
    }
    return {
      value: `${userId.slice(0, MAX_USER_LENGTH)}…`,
      truncated: true,
      full: userId,
    };
  }

  return { value: EMPTY, truncated: false, full: '' };
}

export function DashboardPage() {
  const [recentFlows, setRecentFlows] = useState<LogFlowSummary[]>([]);
  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setIsLoading(true);
        setError(null);

        const [metricsData, flowsPaged] = await Promise.all([
          getDashboardMetrics(),
          getLogFlows({ page: 1, pageSize: RECENT_PAGE_SIZE }),
        ]);

        if (cancelled) return;

        setMetrics(metricsData);
        setRecentFlows(flowsPaged.items);
      } catch {
        if (!cancelled) setError('Unable to load dashboard data.');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="p-6">
      <PageHeader
        title="Logger Dashboard"
        subtitle="Monitor recent flow activity and system logging health"
      />

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
        <MetricCard
          title="Total Flows"
          value={metrics?.totalFlows ?? null}
        />
        <MetricCard
          title="Success Flows"
          value={metrics?.successFlows ?? null}
        />
        <MetricCard
          title="Failed Flows"
          value={metrics?.failedFlows ?? null}
        />
        <MetricCard
          title="Running Flows"
          value={metrics?.runningFlows ?? null}
        />
        <MetricCard
          title="Partial Failed"
          value={metrics?.partialFailed ?? null}
        />
        <MetricCard
          title="Average Duration"
          value={formatDuration(metrics?.averageDurationMs ?? null)}
        />
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Recent Flows</h2>
        </div>

        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center p-12">
              <svg
                className="animate-spin w-8 h-8 text-blue-600 mb-3"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
              <p className="text-sm text-gray-500">Loading dashboard...</p>
            </div>
          ) : error ? (
            <div className="p-6">
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
                <p className="font-medium mb-1">Failed to load dashboard</p>
                <p className="text-sm">{error}</p>
              </div>
            </div>
          ) : recentFlows.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 px-4">
              <p className="text-base font-medium text-gray-700 mb-1">
                No log flows found
              </p>
              <p className="text-sm text-gray-500 text-center max-w-sm">
                Once new flows are processed they will appear here.
              </p>
            </div>
          ) : (
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Flow ID
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Flow Type
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    User
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Last Action
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Last Message
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Updated At
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {recentFlows.map((flow) => {
                  const user = resolveDisplayUser(flow);
                  const lastMessage = flow.lastMessage?.trim();

                  return (
                    <tr key={flow.flowId} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        <Link
                          to={`/logs/${flow.flowId}`}
                          className="text-blue-600 hover:text-blue-800 hover:underline"
                        >
                          {flow.flowId}
                        </Link>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.flowType}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <StatusBadge status={flow.status} />
                      </td>
                      <td
                        className="px-6 py-4 whitespace-nowrap max-w-[240px] text-sm text-gray-700"
                        title={user.truncated ? user.full : undefined}
                      >
                        {user.value}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.lastActionType || EMPTY}
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-600 max-w-[260px]">
                        {lastMessage ? (
                          <div className="truncate" title={lastMessage}>
                            {lastMessage}
                          </div>
                        ) : (
                          <span className="text-gray-400 italic">unknown</span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {formatDate(flow.updatedAt)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
