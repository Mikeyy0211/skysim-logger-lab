import { Link } from 'react-router-dom';
import type { BusinessFlowSummary } from '../types/logFlow';
import { UserCustomerCell } from './UserCustomerCell';
import { StatusBadge } from './StatusBadge';
import { getCurrentStepFromSummary } from '../features/logs/businessFlowSummary';
import { getBusinessStatusLabel } from '../features/logs/businessStatusDisplay';
import { buildListIssueSummary } from '../features/logs/businessFlowSummary';

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? EMPTY : date.toLocaleString();
}

function CompactIdentifiers({
  paymentId,
  transactionId,
}: {
  paymentId: string | null;
  transactionId: string | null;
}) {
  if (!paymentId && !transactionId) return <span className="text-gray-400">{EMPTY}</span>;
  return (
    <div className="flex flex-col gap-0.5 text-xs text-gray-600">
      {paymentId && <span className="truncate" title={paymentId}>Thanh toán: <span className="font-mono">{paymentId}</span></span>}
      {transactionId && <span className="truncate" title={transactionId}>Giao dịch: <span className="font-mono">{transactionId}</span></span>}
    </div>
  );
}

export function BusinessLogTable({ flows }: { flows: BusinessFlowSummary[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[980px]">
        <thead>
          <tr className="bg-gray-50">
            {['Cập nhật gần nhất', 'Mã đơn hàng', 'Người dùng / Khách hàng', 'Thanh toán / Giao dịch', 'Bước gần nhất', 'Trạng thái', 'Vấn đề', 'Thao tác'].map((label) => (
              <th key={label} className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {flows.map((flow) => {
            const currentStep = getCurrentStepFromSummary(flow.lastActionType, flow.lastMessage);
            const problem = buildListIssueSummary(
              flow.overallStatus,
              flow.attentionActionType ?? flow.lastActionType,
              flow.attentionMessage ?? flow.lastMessage,
              flow.issueSummary,
            );
            return (
              <tr key={flow.orderCode} className="hover:bg-gray-50 align-top">
                <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-500">{formatDate(flow.lastSeenAt)}</td>
                <td className="px-4 py-4 max-w-[190px] text-sm font-mono font-medium text-gray-900">
                  <span className="truncate block" title={flow.orderCode}>{flow.orderCode}</span>
                </td>
                <td className="px-4 py-4 max-w-[220px]"><UserCustomerCell userEmail={flow.userEmail} customerEmail={flow.customerEmail} userId={flow.userId} /></td>
                <td className="px-4 py-4 max-w-[230px]"><CompactIdentifiers paymentId={flow.paymentId} transactionId={flow.transactionId} /></td>
                <td className="px-4 py-4 max-w-[220px] text-sm text-gray-700">
                  <span className="truncate block" title={currentStep}>{currentStep}</span>
                </td>
                <td className="px-4 py-4 whitespace-nowrap" title={flow.overallStatus}>
                  <StatusBadge status={flow.overallStatus} friendly />
                  <span className="sr-only">{getBusinessStatusLabel(flow.overallStatus)}</span>
                </td>
                <td className="px-4 py-4 max-w-[260px] text-sm text-gray-600">
                  <span className="truncate block" title={problem !== EMPTY ? problem : undefined}>{problem}</span>
                </td>
                <td className="px-4 py-4 whitespace-nowrap text-sm">
                  <Link
                    to={`/business-flows/${encodeURIComponent(flow.orderCode)}`}
                    className="text-blue-600 hover:text-blue-800 font-medium"
                  >
                    Xem hành trình
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
