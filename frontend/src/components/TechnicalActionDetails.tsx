import { useEffect, useState } from 'react';
import type { BusinessFlowAction } from '../types/logFlow';
import type { LogActionDetailsResponse } from '../types/logAction';
import { getLogActionDetails } from '../services/businessFlowService';
import { getStatusCode, getTechnicalPath } from '../features/logs/businessActionMapping';

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

function formatDuration(value: number | null | undefined): string {
  if (value == null || value < 0) return '—';
  return value >= 1000 ? `${(value / 1000).toFixed(1)}s` : `${value}ms`;
}

function maskSensitiveText(value: string): string {
  return value.replace(/(Bearer\s+)[^\s"']+/gi, '$1***');
}

function PayloadBlock({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null;
  return (
    <details className="border border-gray-200 rounded-lg bg-white">
      <summary className="cursor-pointer px-3 py-2 text-xs font-medium text-gray-700">
        {label}
      </summary>
      <pre className="border-t border-gray-200 bg-gray-50 p-3 text-xs leading-relaxed font-mono whitespace-pre-wrap break-words max-h-80 overflow-auto">
        {maskSensitiveText(value)}
      </pre>
    </details>
  );
}

export function TechnicalActionDetails({ action }: { action: BusinessFlowAction }) {
  const [isOpen, setIsOpen] = useState(false);
  const [details, setDetails] = useState<LogActionDetailsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!isOpen || details || !action.actionId) return;

    setIsLoading(true);
    setError(false);
    void getLogActionDetails(action.actionId)
      .then(setDetails)
      .catch(() => setError(true))
      .finally(() => setIsLoading(false));
  }, [action.actionId, details, isOpen]);

  const path = getTechnicalPath(action.message);
  const statusCode = getStatusCode(action.message, action.errorCode);

  return (
    <div className="mt-3 border-t border-gray-100 pt-3">
      <button
        type="button"
        onClick={() => setIsOpen((previous) => !previous)}
        className="text-xs font-medium text-blue-600 hover:text-blue-800 focus:outline-none focus:underline"
      >
        {isOpen ? 'Ẩn dữ liệu kỹ thuật' : 'Xem dữ liệu kỹ thuật'}
      </button>

      {isOpen && (
        <div className="mt-3 space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
            <div><span className="text-gray-500">Dịch vụ</span><p className="font-medium text-gray-800">{action.serviceName || '—'}</p></div>
            <div><span className="text-gray-500">Endpoint</span><p className="font-mono text-gray-800 break-all">{path || '—'}</p></div>
            <div><span className="text-gray-500">HTTP status</span><p className="font-mono text-gray-800">{statusCode ?? '—'}</p></div>
            <div><span className="text-gray-500">Thời gian xử lý</span><p className="font-mono text-gray-800">{formatDuration(action.durationMs)}</p></div>
            <div><span className="text-gray-500">FlowId</span><p className="font-mono text-gray-800 break-all">{action.flowId || '—'}</p></div>
            <div><span className="text-gray-500">CorrelationId</span><p className="font-mono text-gray-800 break-all">{action.correlationId || '—'}</p></div>
            <div><span className="text-gray-500">EventId</span><p className="font-mono text-gray-800 break-all">{action.eventId || '—'}</p></div>
            <div><span className="text-gray-500">Thời gian</span><p className="text-gray-800">{formatDate(action.createdAt)}</p></div>
          </div>

          {isLoading && <p className="text-xs text-gray-500">Đang tải dữ liệu kỹ thuật…</p>}
          {error && <p className="text-xs text-red-600">Không thể tải dữ liệu kỹ thuật.</p>}
          {details && (
            <div className="space-y-2">
              <PayloadBlock label="Dữ liệu request" value={details.requestPayload} />
              <PayloadBlock label="Dữ liệu response" value={details.responsePayload} />
              <PayloadBlock label="Dữ liệu lỗi" value={details.errorPayload} />
              <PayloadBlock label="Metadata" value={details.metadata} />
              {!details.requestPayload && !details.responsePayload && !details.errorPayload && !details.metadata && (
                <p className="text-xs text-gray-400 italic">Không có dữ liệu payload.</p>
              )}
            </div>
          )}
          {!action.actionId && <p className="text-xs text-gray-400">Không có mã hành động.</p>}
        </div>
      )}
    </div>
  );
}
