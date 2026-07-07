import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { MetricCard } from '../components/MetricCard';
import { StatusBadge } from '../components/StatusBadge';
import { getDashboardMetrics } from '../services/logFlowService';
import type { DashboardMetrics, RecentFlowItem } from '../types/logFlow';

const EMPTY = '—';

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

// Priority: username -> userEmail -> customerEmail -> userId -> —
function resolveUser(item: RecentFlowItem): string {
  return (
    item.username?.trim() ||
    item.userEmail?.trim() ||
    item.customerEmail?.trim() ||
    item.userId?.trim() ||
    EMPTY
  );
}

// Priority: orderCode -> orderId -> paymentId -> transactionId -> —
function resolveOrder(item: RecentFlowItem): string {
  return (
    item.orderCode?.trim() ||
    item.orderId?.trim() ||
    item.paymentId?.trim() ||
    item.transactionId?.trim() ||
    EMPTY
  );
}

function resolveService(item: RecentFlowItem): string {
  return item.lastServiceName?.trim() || EMPTY;
}

function resolveAction(item: RecentFlowItem): string {
  return item.lastActionType?.trim() || EMPTY;
}

function resolveMessage(item: RecentFlowItem): string {
  return item.lastMessage?.trim() || EMPTY;
}

function resolveTime(item: RecentFlowItem): string {
  return formatDate(item.updatedAt || item.createdAt);
}

function LoadingSpinner({ label }: { label: string }) {
  return (
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
      <p className="text-sm text-gray-500">{label}</p>
    </div>
  );
}

function ErrorBanner({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="p-6">
      <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
        <p className="font-medium mb-1">Failed to load dashboard</p>
        <p className="text-sm">{message}</p>
        <button
          onClick={onRetry}
          className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
        >
          Retry
        </button>
      </div>
    </div>
  );
}

function EmptySection({ message }: { message: string }) {
  return (
    <div className="px-6 py-10 text-center">
      <p className="text-sm text-gray-500">{message}</p>
    </div>
  );
}

function RecentFlowRow({ item }: { item: RecentFlowItem }) {
  const user = resolveUser(item);
  const order = resolveOrder(item);
  const service = resolveService(item);
  const action = resolveAction(item);
  const message = resolveMessage(item);
  const time = resolveTime(item);

  return (
    <tr className="hover:bg-gray-50">
      <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-700">
        <span className="truncate inline-block max-w-[200px]" title={user !== EMPTY ? user : undefined}>
          {user}
        </span>
      </td>
      <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-700 font-mono">
        <span className="truncate inline-block max-w-[200px]" title={order !== EMPTY ? order : undefined}>
          {order}
        </span>
      </td>
      <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
        {service !== EMPTY ? service : <span className="text-gray-400 italic">unknown</span>}
      </td>
      <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">{action}</td>
      <td className="px-4 py-3 whitespace-nowrap">
        <StatusBadge status={item.status} />
      </td>
      <td className="px-4 py-3 text-sm text-gray-600 max-w-[240px]">
        {message !== EMPTY ? (
          <div className="truncate" title={message}>
            {message}
          </div>
        ) : (
          <span className="text-gray-400 italic">unknown</span>
        )}
      </td>
      <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-500">{time}</td>
      <td className="px-4 py-3 whitespace-nowrap text-sm">
        <Link
          to={`/logs/${item.flowId}`}
          className="text-blue-600 hover:text-blue-800 font-medium"
        >
          View
        </Link>
      </td>
    </tr>
  );
}

function RecentFlowSection({
  title,
  items,
  emptyMessage,
}: {
  title: string;
  items: RecentFlowItem[];
  emptyMessage: string;
}) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
      <div className="px-6 py-4 border-b border-gray-200">
        <h2 className="text-lg font-semibold text-gray-900">{title}</h2>
      </div>
      {items.length === 0 ? (
        <EmptySection message={emptyMessage} />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  User
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Order
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Service
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Action
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Status
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Message
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Time
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Detail
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {items.map((item) => (
                <RecentFlowRow key={item.flowId} item={item} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function MetricCardsGrid({ metrics }: { metrics: DashboardMetrics }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <MetricCard title="Total Flows" value={metrics.totalFlows} />
      <MetricCard title="Total Logs / Actions" value={metrics.totalActions} />
      <MetricCard title="Logs Today" value={metrics.logsToday} />
      <MetricCard title="Logs This Week" value={metrics.logsThisWeek} />
      <MetricCard title="Failed Flows" value={metrics.failedFlows} />
      <MetricCard title="Success Rate" value={`${metrics.successRate.toFixed(1)}%`} />
    </div>
  );
}

export function DashboardPage() {
  const navigate = useNavigate();
  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  async function load() {
    try {
      setIsLoading(true);
      setError(null);
      const data = await getDashboardMetrics();
      setMetrics(data);
    } catch {
      setError('Unable to load dashboard data.');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function handleSearchSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const keyword = search.trim();
    if (!keyword) return;
    navigate(`/logs?keyword=${encodeURIComponent(keyword)}`);
  }

  return (
    <div className="p-6 space-y-6">
      <PageHeader
        title="Logger Dashboard"
        subtitle="Operational health snapshot of the logger system"
      />

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-4">
        <form
          onSubmit={handleSearchSubmit}
          className="flex flex-col sm:flex-row sm:items-end gap-3"
        >
          <div className="flex-1">
            <label
              htmlFor="quickSearch"
              className="block text-sm font-medium text-gray-700 mb-1"
            >
              Quick Search
            </label>
            <input
              id="quickSearch"
              type="text"
              placeholder="Search by order code, payment ID, transaction ID..."
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <button
            type="submit"
            disabled={!search.trim()}
            className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Search
          </button>
        </form>
      </div>

      {isLoading ? (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
          <LoadingSpinner label="Loading dashboard..." />
        </div>
      ) : error ? (
        <ErrorBanner message={error} onRetry={load} />
      ) : metrics ? (
        <>
          <MetricCardsGrid metrics={metrics} />

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <RecentFlowSection
              title="Recent Failed Flows"
              items={metrics.recentFailedFlows}
              emptyMessage="No failed flows yet."
            />
            <RecentFlowSection
              title="Recent Successful Flows"
              items={metrics.recentSuccessFlows}
              emptyMessage="No successful flows yet."
            />
          </div>

          <div className="text-xs text-gray-500">
            Average flow duration: {formatDuration(metrics.averageDurationMs)} ·
            Running: {metrics.runningFlows} · Partial Failed: {metrics.partialFailed} ·
            Success: {metrics.successFlows}
          </div>
        </>
      ) : null}
    </div>
  );
}