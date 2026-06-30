import { Link } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { MetricCard } from '../components/MetricCard';
import { StatusBadge } from '../components/StatusBadge';
import { mockDashboardMetrics, mockFlows } from '../data/mockData';

function formatDuration(ms: number): string {
  if (ms >= 1000) {
    return `${(ms / 1000).toFixed(1)}s`;
  }
  return `${ms}ms`;
}

export function DashboardPage() {
  const recentFlows = mockFlows.slice(0, 5);

  return (
    <div className="p-6">
      <PageHeader
        title="Logger Dashboard"
        subtitle="Monitor recent flow activity and system logging health"
      />

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
        <MetricCard title="Total Flows" value={mockDashboardMetrics.totalFlows} />
        <MetricCard title="Success Flows" value={mockDashboardMetrics.successFlows} />
        <MetricCard title="Failed Flows" value={mockDashboardMetrics.failedFlows} />
        <MetricCard title="Running Flows" value={mockDashboardMetrics.runningFlows} />
        <MetricCard title="Partial Failed" value={mockDashboardMetrics.partialFailed} />
        <MetricCard
          title="Average Duration"
          value={formatDuration(mockDashboardMetrics.averageDurationMs)}
        />
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Recent Flows</h2>
        </div>
        <div className="overflow-x-auto">
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
              {recentFlows.map((flow) => (
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
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                    {flow.lastActionType}
                  </td>
                  <td className="px-6 py-4 text-sm text-gray-600 max-w-xs truncate">
                    {flow.lastMessage}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {new Date(flow.updatedAt).toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
