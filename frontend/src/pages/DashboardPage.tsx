import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { MetricCard } from '../components/MetricCard';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { UserCustomerCell } from '../components/UserCustomerCell';
import { buildListIssueSummary } from '../features/logs/businessFlowSummary';
import { getCurrentStepDisplay } from '../features/logs/businessActionMapping';
import { getBusinessDashboardSummary } from '../services/businessFlowService';
import type { BusinessDashboardOrder, BusinessDashboardSummary } from '../types/logFlow';

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? EMPTY : date.toLocaleString();
}

function PaymentTransactionCell({ item }: { item: BusinessDashboardOrder }) {
  if (!item.paymentId && !item.transactionId) return <span className="text-gray-400">{EMPTY}</span>;

  return (
    <div className="flex flex-col gap-0.5 text-xs text-gray-600">
      {item.paymentId && <span className="truncate" title={item.paymentId}>Thanh toán: <span className="font-mono">{item.paymentId}</span></span>}
      {item.transactionId && <span className="truncate" title={item.transactionId}>Giao dịch: <span className="font-mono">{item.transactionId}</span></span>}
    </div>
  );
}

function LoadingState() {
  return (
    <div className="bg-white rounded-lg border border-gray-200 shadow-sm flex flex-col items-center justify-center p-12">
      <svg className="animate-spin w-8 h-8 text-blue-600 mb-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" aria-hidden="true">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
      </svg>
      <p className="text-sm text-gray-500">Đang tải tổng quan...</p>
    </div>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
      <p className="font-medium mb-1">Không thể tải dữ liệu tổng quan</p>
      <p className="text-sm">Vui lòng thử lại.</p>
      <button type="button" onClick={onRetry} className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200">
        Thử lại
      </button>
    </div>
  );
}

function BusinessOrderTable({
  title,
  orders,
  requiresAttention,
}: {
  title: string;
  orders: BusinessDashboardOrder[];
  requiresAttention: boolean;
}) {
  return (
    <section className="bg-white rounded-lg border border-gray-200 shadow-sm">
      <div className="px-6 py-4 border-b border-gray-200">
        <h2 className="text-lg font-semibold text-gray-900">{title}</h2>
      </div>
      {orders.length === 0 ? (
        <p className="px-6 py-10 text-center text-sm text-gray-500">{requiresAttention ? 'Chưa có đơn hàng cần kiểm tra.' : 'Chưa có đơn hàng hoàn tất.'}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1100px]">
            <thead>
              <tr className="bg-gray-50">
                {(requiresAttention
                  ? ['Mã đơn hàng', 'Người dùng / Khách hàng', 'Thanh toán / Giao dịch', 'Bước cần kiểm tra', 'Vấn đề', 'Cập nhật gần nhất', 'Thao tác']
                  : ['Mã đơn hàng', 'Người dùng / Khách hàng', 'Thanh toán / Giao dịch', 'Bước gần nhất', 'Trạng thái', 'Cập nhật gần nhất', 'Thao tác']
                ).map((label) => (
                  <th key={label} className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {orders.map((order) => {
                const stepActionType = requiresAttention ? order.attentionActionType : order.lastActionType;
                const stepMessage = requiresAttention ? order.attentionMessage : order.lastMessage;
                const currentStep = getCurrentStepDisplay(stepActionType, stepMessage);
                const issue = buildListIssueSummary(order.status, stepActionType, stepMessage, order.issueSummary);
                return (
                  <tr key={order.orderCode} className="hover:bg-gray-50 align-top">
                    <td className="px-4 py-4 max-w-[190px] text-sm font-mono font-medium text-gray-900">
                      <span className="truncate block" title={order.orderCode}>{order.orderCode}</span>
                    </td>
                    <td className="px-4 py-4 max-w-[240px]"><UserCustomerCell userEmail={order.userEmail} customerEmail={order.customerEmail} /></td>
                    <td className="px-4 py-4 max-w-[220px]"><PaymentTransactionCell item={order} /></td>
                    <td className="px-4 py-4 max-w-[250px] text-sm text-gray-700"><span className="truncate block" title={currentStep}>{currentStep}</span></td>
                    <td className="px-4 py-4 max-w-[260px] text-sm text-gray-600">
                      {requiresAttention ? <span className="truncate block" title={issue}>{issue}</span> : <StatusBadge status={order.status} friendly />}
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-500">{formatDate(order.lastSeen)}</td>
                    <td className="px-4 py-4 whitespace-nowrap text-sm">
                      <Link to={`/business-flows/${encodeURIComponent(order.orderCode)}`} className="text-blue-600 hover:text-blue-800 font-medium">
                        Xem hành trình
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function TechnicalHealth({ summary }: { summary: BusinessDashboardSummary['technicalSummary'] }) {
  return (
    <section className="bg-slate-50 border border-slate-200 rounded-lg p-5">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-4">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">Sức khỏe kỹ thuật</h2>
          <p className="text-sm text-gray-500 mt-1">Các chỉ số kỹ thuật được giữ ở phần phụ để hỗ trợ gỡ lỗi.</p>
        </div>
        <Link to="/logs?tab=technical" className="text-sm font-medium text-blue-600 hover:text-blue-800">Xem nhật ký kỹ thuật</Link>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        <MetricCard title="Tổng luồng kỹ thuật" value={summary.totalFlows} />
        <MetricCard title="Tổng hành động log" value={summary.totalActions} />
        <MetricCard title="Log kỹ thuật hôm nay" value={summary.logsToday} />
        <MetricCard title="Luồng kỹ thuật lỗi" value={summary.failedFlows} />
        <MetricCard title="Tỷ lệ thành công kỹ thuật" value={`${summary.successRate.toFixed(1)}%`} />
      </div>
    </section>
  );
}

export function DashboardPage() {
  const navigate = useNavigate();
  const [summary, setSummary] = useState<BusinessDashboardSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);
  const [search, setSearch] = useState('');

  async function load() {
    setIsLoading(true);
    setError(false);
    try {
      setSummary(await getBusinessDashboardSummary());
    } catch {
      setError(true);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  function handleSearchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const keyword = search.trim();
    if (keyword) navigate(`/logs?tab=business&keyword=${encodeURIComponent(keyword)}`);
  }

  return (
    <div className="p-6 space-y-6">
      <PageHeader title="Tổng quan vận hành" subtitle="Theo dõi đơn hàng, trạng thái xử lý và các vấn đề cần kiểm tra" />

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm p-4">
        <form onSubmit={handleSearchSubmit} className="flex flex-col sm:flex-row sm:items-end gap-3">
          <div className="flex-1">
            <label htmlFor="quickSearch" className="block text-sm font-medium text-gray-700 mb-1">Tra cứu nhanh</label>
            <input
              id="quickSearch"
              type="text"
              placeholder="Tìm theo mã đơn hàng, mã thanh toán, mã giao dịch, email hoặc số điện thoại..."
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
          <button type="submit" disabled={!search.trim()} className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
            Tìm kiếm
          </button>
        </form>
      </section>

      {isLoading ? <LoadingState /> : error ? <ErrorState onRetry={load} /> : summary ? (
        <>
          <section>
            <h2 className="sr-only">Chỉ số đơn hàng</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <MetricCard title="Tổng đơn hàng" value={summary.totalOrders} />
              <MetricCard title="Đơn hàng hôm nay" value={summary.ordersToday} />
              <MetricCard title="Đang xử lý" value={summary.runningOrders} />
              <MetricCard title="Cần kiểm tra" value={summary.requiresAttentionOrders} />
              <MetricCard title="Hoàn tất" value={summary.completedOrders} />
              <MetricCard title="Tỷ lệ hoàn tất" value={`${summary.completionRate.toFixed(1)}%`} />
            </div>
          </section>

          <BusinessOrderTable title="Đơn hàng cần kiểm tra gần đây" orders={summary.recentRequiresAttention} requiresAttention />
          <BusinessOrderTable title="Đơn hàng hoàn tất gần đây" orders={summary.recentCompleted} requiresAttention={false} />
          <TechnicalHealth summary={summary.technicalSummary} />
        </>
      ) : null}
    </div>
  );
}
