import { useMemo } from 'react';
import type { BusinessFlowAction } from '../types/logFlow';
import { getBusinessActionDisplay, getStatusCode } from '../features/logs/businessActionMapping';
import { getBusinessStatusLabel, normalizeBusinessStatus } from '../features/logs/businessStatusDisplay';
import { TechnicalActionDetails } from './TechnicalActionDetails';

function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

function formatActionError(action: BusinessFlowAction): string | null {
  const code = getStatusCode(action.message, action.errorCode);
  if (code !== null) return String(code);
  return action.errorMessage?.trim() || null;
}

function actionKey(action: BusinessFlowAction): string {
  return getBusinessActionDisplay(action.actionType, action.message).title;
}

export function BusinessFlowJourney({ actions }: { actions: BusinessFlowAction[] }) {
  const repeatedCounts = useMemo(() => {
    const counts = new Map<string, number>();
    actions.forEach((action) => counts.set(actionKey(action), (counts.get(actionKey(action)) ?? 0) + 1));
    return counts;
  }, [actions]);
  const seenCounts = new Map<string, number>();

  return (
    <section className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
        <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
          Hành trình xử lý đơn hàng ({actions.length} bước)
        </h2>
      </div>
      <div className="p-6">
        {actions.length === 0 ? (
          <p className="py-8 text-center text-sm text-gray-500">Không có bước xử lý cho đơn hàng này.</p>
        ) : (
          <div className="space-y-0">
            {actions.map((action, index) => {
              const display = getBusinessActionDisplay(action.actionType, action.message);
              const normalizedStatus = normalizeBusinessStatus(action.status);
              const key = actionKey(action);
              const occurrence = (seenCounts.get(key) ?? 0) + 1;
              seenCounts.set(key, occurrence);
              const repeated = (repeatedCounts.get(key) ?? 0) > 1;
              const error = formatActionError(action);

              return (
                <div key={`${action.flowId}-${action.eventId}-${action.actionId}`} className="flex gap-3">
                  <div className="flex flex-col items-center w-7 flex-shrink-0">
                    <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold ${
                      normalizedStatus === 'FAILED' || normalizedStatus === 'ERROR' || normalizedStatus === 'TIMEOUT'
                        ? 'bg-red-100 text-red-700 ring-2 ring-red-200'
                        : normalizedStatus === 'SUCCESS'
                          ? 'bg-green-100 text-green-700'
                          : 'bg-blue-100 text-blue-700'
                    }`}>
                      {normalizedStatus === 'FAILED' || normalizedStatus === 'ERROR' || normalizedStatus === 'TIMEOUT' ? '×' : '✓'}
                    </div>
                    {index < actions.length - 1 && <div className="w-px flex-1 bg-gray-200 my-1" />}
                  </div>
                  <div className="flex-1 min-w-0 pb-4">
                    <div className="border border-gray-200 rounded-lg p-4">
                      <div className="flex items-start justify-between gap-3 flex-wrap">
                        <div>
                          <h3 className="text-sm font-semibold text-gray-900">
                            {display.title}{repeated ? ` — lần ${occurrence}` : ''}
                          </h3>
                          <p className="text-xs text-gray-500 mt-1">
                            {getBusinessStatusLabel(action.status)} · {formatDate(action.createdAt)}
                          </p>
                        </div>
                        {error && <span className="text-xs font-medium text-red-700">{error}</span>}
                      </div>
                      {normalizedStatus === 'FAILED' || normalizedStatus === 'ERROR' || normalizedStatus === 'TIMEOUT' ? (
                        <p className="text-sm text-red-700 mt-3">{display.title} cần được kiểm tra kỹ thuật.</p>
                      ) : normalizedStatus === 'SUCCESS' ? (
                        <p className="text-sm text-gray-600 mt-3">{display.title} đã hoàn tất.</p>
                      ) : (
                        <p className="text-sm text-gray-600 mt-3">{display.title} đang được xử lý.</p>
                      )}
                      <TechnicalActionDetails action={action} />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
