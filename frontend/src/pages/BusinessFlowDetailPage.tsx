import React, { useState, useEffect, useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { StatusBadge } from '../components/StatusBadge';
import { getBusinessFlowByOrderCode } from '../services/businessFlowService';
import type { BusinessFlowDetail, BusinessFlowAction } from '../types/logFlow';

const EMPTY = '—';

function formatDuration(ms: number | null | undefined): string {
  if (ms == null || ms < 0) return '';
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${ms}ms`;
}

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  try {
    return new Date(value).toLocaleString();
  } catch {
    return EMPTY;
  }
}

function prettyJson(value: unknown): string {
  if (value === undefined) return '';
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function parseJsonSafely(raw: string | null | undefined): unknown | null {
  if (raw === null || raw === undefined || raw === '') return null;
  if (typeof raw !== 'string') return raw;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function maskAuthorization(value: string): string {
  if (typeof value !== 'string' || value.length === 0) return value;
  return value.replace(/(Bearer\s+)[^\s"']+/gi, '$1***');
}

// ─── Layout Helpers ─────────────────────────────────────────────────────────────

function FieldRow({
  label,
  value,
  isMonospace = false,
}: {
  label: string;
  value: React.ReactNode;
  isMonospace?: boolean;
}) {
  const valueStr = typeof value === 'string' ? value : '';
  return (
    <div className="flex flex-col gap-1 min-w-0">
      <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
        {label}
      </span>
      <span
        className={`text-sm text-gray-900 min-w-0 ${isMonospace ? 'font-mono break-all' : ''}`}
        title={valueStr.length > 60 ? valueStr : undefined}
      >
        {value}
      </span>
    </div>
  );
}

function MetricPill({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex flex-col gap-1 min-w-0">
      <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
        {label}
      </span>
      <span className="text-sm font-semibold text-gray-900">{value}</span>
    </div>
  );
}

function LoadingSkeleton() {
  return (
    <div className="p-6 space-y-6">
      <div className="h-4 w-24 bg-gray-200 rounded animate-pulse" />
      <div className="space-y-4">
        <div className="h-8 w-48 bg-gray-200 rounded animate-pulse" />
        <div className="h-4 w-32 bg-gray-200 rounded animate-pulse" />
      </div>
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <div className="h-6 w-40 bg-gray-200 rounded mb-6 animate-pulse" />
        <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="space-y-2">
              <div className="h-3 w-20 bg-gray-200 rounded animate-pulse" />
              <div className="h-4 w-28 bg-gray-200 rounded animate-pulse" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ErrorCard({
  title,
  message,
  onRetry,
  backLink,
}: {
  title: string;
  message: string;
  onRetry: () => void;
  backLink?: string;
}) {
  return (
    <div className="p-6">
      {backLink && (
        <Link
          to={backLink}
          className="inline-flex items-center text-sm text-gray-600 hover:text-gray-900 mb-4"
        >
          <svg
            className="w-4 h-4 mr-1"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M15 19l-7-7 7-7"
            />
          </svg>
          Back to Logs
        </Link>
      )}
      <div className="bg-red-50 border border-red-200 rounded-lg p-6">
        <p className="text-red-700 font-semibold">{title}</p>
        <p className="text-red-600 text-sm mt-1">{message}</p>
        <button
          onClick={onRetry}
          className="mt-4 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
        >
          Retry
        </button>
      </div>
    </div>
  );
}

function CompactRawBlock({ value }: { value: unknown }) {
  return (
    <pre className="text-xs text-gray-800 bg-gray-50 p-3 rounded border border-gray-200 font-mono leading-relaxed max-w-full max-h-72 overflow-x-auto overflow-y-auto whitespace-pre-wrap break-words">
      {prettyJson(value)}
    </pre>
  );
}

function RequestSummaryView({ raw }: { raw: string | null | undefined }) {
  const [showFullHeaders, setShowFullHeaders] = useState(false);

  if (!raw) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return <CompactRawBlock value={raw} />;
  }

  const record = parsed as Record<string, unknown>;
  const method = record.method as string | undefined;
  const path = record.path as string | undefined;
  const fullUrl = record.fullUrl as string | undefined;
  const clientIp = record.clientIp as string | undefined;
  const contentType = record.contentType as string | undefined;

  const headersRaw =
    record.headers !== undefined
      ? record.headers
      : record.requestHeaders !== undefined
        ? record.requestHeaders
        : undefined;

  const bodyValue =
    record.body !== undefined
      ? record.body
      : record.requestBody !== undefined
        ? record.requestBody
        : record.requestPayload !== undefined
          ? record.requestPayload
          : undefined;

  const headers = typeof headersRaw === 'object' && headersRaw !== null
    ? (headersRaw as Record<string, string>)
    : null;
  const hasAuthHeader = headers && Object.keys(headers).some((k) => k.toLowerCase() === 'authorization');

  const displayUrl = (() => {
    if (!fullUrl) return null;
    if (fullUrl.length <= 100) return fullUrl;
    return fullUrl.substring(0, 100) + '…';
  })();

  const bodyPreview = (() => {
    if (!bodyValue) return null;
    const str = typeof bodyValue === 'string' ? bodyValue : prettyJson(bodyValue);
    if (str.length <= 400) return str;
    return str.substring(0, 400) + '…';
  })();

  const hasStructured = method || path || displayUrl || clientIp || contentType || bodyPreview;

  if (!hasStructured) {
    return <CompactRawBlock value={parsed} />;
  }

  return (
    <div className="space-y-2 min-w-0">
      {(method || path || displayUrl || clientIp) && (
        <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-x-2 gap-y-1 text-xs items-baseline min-w-0">
          {method && (
            <>
              <span className="text-gray-500 truncate">Method</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{method}</span>
            </>
          )}
          {path && (
            <>
              <span className="text-gray-500 truncate">Path</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{path}</span>
            </>
          )}
          {displayUrl && (
            <>
              <span className="text-gray-500 truncate">Full URL</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{displayUrl}</span>
            </>
          )}
          {clientIp && (
            <>
              <span className="text-gray-500 truncate">Client IP</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{clientIp}</span>
            </>
          )}
          {contentType && (
            <>
              <span className="text-gray-500 truncate">Content-Type</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{contentType}</span>
            </>
          )}
          {hasAuthHeader && (
            <>
              <span className="text-gray-500 truncate">Authorization</span>
              <span className="text-gray-900 font-mono break-all min-w-0">***</span>
            </>
          )}
        </div>
      )}

      {bodyPreview && (
        <div className="min-w-0">
          <p className="text-xs text-gray-500 mb-1">Body</p>
          <pre className="text-xs text-gray-800 bg-gray-50 p-2 rounded border border-gray-200 font-mono whitespace-pre-wrap break-words max-w-full overflow-x-auto">
            {bodyPreview}
          </pre>
        </div>
      )}

      {headers && Object.keys(headers).length > 0 && (
        <div className="min-w-0">
          <button
            onClick={() => setShowFullHeaders((prev) => !prev)}
            className="text-xs text-blue-600 hover:text-blue-800 font-medium focus:outline-none focus:underline"
          >
            {showFullHeaders ? 'Hide' : 'Show'} full headers ({Object.keys(headers).length})
          </button>
          {showFullHeaders && (
            <div className="mt-1 bg-white rounded border border-gray-200 p-2 max-h-64 overflow-y-auto max-w-full">
              {Object.entries(headers).map(([k, v]) => {
                const key = k.toLowerCase();
                const masked = key === 'authorization' ? '***' : maskAuthorization(typeof v === 'string' ? v : String(v));
                return (
                  <div
                    key={k}
                    className="grid grid-cols-[8rem_minmax(0,1fr)] gap-x-2 text-xs py-0.5 items-baseline min-w-0"
                  >
                    <span className="text-gray-500 truncate">{k}</span>
                    <span className="text-gray-900 font-mono break-all min-w-0">{masked}</span>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ResponseSummaryView({ raw }: { raw: string | null | undefined }) {
  if (!raw) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return <CompactRawBlock value={raw} />;
  }

  const record = parsed as Record<string, unknown>;
  const statusCode = record.statusCode as number | undefined;
  const durationMs = record.durationMs as number | undefined;
  const bodyValue =
    record.body !== undefined
      ? record.body
      : record.responseBody !== undefined
        ? record.responseBody
        : undefined;

  const bodyPreview = (() => {
    if (!bodyValue) return null;
    const str = typeof bodyValue === 'string' ? bodyValue : prettyJson(bodyValue);
    if (str.length <= 400) return str;
    return str.substring(0, 400) + '…';
  })();

  return (
    <div className="space-y-2 min-w-0">
      {(statusCode !== undefined || durationMs !== undefined) && (
        <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-x-2 gap-y-1 text-xs items-baseline min-w-0">
          {statusCode !== undefined && (
            <>
              <span className="text-gray-500 truncate">Status Code</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{statusCode}</span>
            </>
          )}
          {durationMs !== undefined && (
            <>
              <span className="text-gray-500 truncate">Duration</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{durationMs}ms</span>
            </>
          )}
        </div>
      )}

      {bodyPreview && (
        <div className="min-w-0">
          <p className="text-xs text-gray-500 mb-1">Body</p>
          <pre className="text-xs text-gray-800 bg-gray-50 p-2 rounded border border-gray-200 font-mono whitespace-pre-wrap break-words max-w-full overflow-x-auto">
            {bodyPreview}
          </pre>
        </div>
      )}
    </div>
  );
}

// ─── Action Detail Panel ────────────────────────────────────────────────────────

function hasInlinePayload(action: BusinessFlowAction): boolean {
  return !!(action.requestPayload || action.responsePayload || action.errorPayload || action.metadata);
}

function ActionPayloadsPanel({ action }: { action: BusinessFlowAction }) {
  const hasPayload = hasInlinePayload(action);

  if (!hasPayload) {
    return <p className="text-xs text-gray-400 italic">No detail payload available.</p>;
  }

  return (
    <div className="space-y-4">
      <div>
        <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
          Request
        </h4>
        {action.requestPayload ? (
          <RequestSummaryView raw={action.requestPayload} />
        ) : (
          <p className="text-xs text-gray-400 italic">—</p>
        )}
      </div>

      <div>
        <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
          Response
        </h4>
        {action.responsePayload ? (
          <ResponseSummaryView raw={action.responsePayload} />
        ) : (
          <p className="text-xs text-gray-400 italic">—</p>
        )}
      </div>

      {action.errorPayload && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Error
          </h4>
          <CompactRawBlock
            value={parseJsonSafely(action.errorPayload) ?? action.errorPayload}
          />
        </div>
      )}

      {action.metadata && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Raw Metadata
          </h4>
          <CompactRawBlock value={parseJsonSafely(action.metadata) ?? action.metadata} />
        </div>
      )}
    </div>
  );
}

// ─── Timeline Item ──────────────────────────────────────────────────────────────

function isFailedAction(action: BusinessFlowAction): boolean {
  const s = action.status?.toUpperCase();
  return s === 'FAILED' || s === 'ERROR' || s === 'TIMEOUT';
}

/** Parse duration from message text like "POST /api -> 200 (22ms)" */
function parseDurationFromMessage(message: string | null | undefined): number | null {
  if (!message) return null;
  const match = message.match(/\((\d+)(ms|s)\)/);
  if (match) {
    const num = parseInt(match[1], 10);
    if (match[2] === 's') return num * 1000;
    return num;
  }
  return null;
}

/**
 * Build timeline metadata items array.
 * Each entry is either a JSX element or null (filtered out before render).
 * Separators " · " are added between non-null items at render time.
 */
function buildTimelineMetaItems(action: BusinessFlowAction): React.ReactNode[] {
  const items: React.ReactNode[] = [];

  if (action.serviceName) {
    items.push(
      <span key="svc" className="font-medium text-gray-600">{action.serviceName}</span>
    );
  }

  const duration = action.durationMs ?? parseDurationFromMessage(action.message);
  if (duration != null) {
    items.push(
      <span key="dur">{formatDuration(duration)}</span>
    );
  }

  const dateStr = formatDate(action.createdAt);
  if (dateStr !== EMPTY) {
    items.push(
      <span key="time">{dateStr}</span>
    );
  }

  if (action.flowId) {
    items.push(
      <span key="fid" className="font-mono text-gray-400" title={`Flow ID: ${action.flowId}`}>
        {action.flowId.length > 16 ? action.flowId.substring(0, 16) + '…' : action.flowId}
      </span>
    );
  }

  return items;
}

function BusinessActionTimelineItem({
  action,
  stepNumber,
  isSelected,
  onToggle,
}: {
  action: BusinessFlowAction;
  stepNumber: number;
  isSelected: boolean;
  onToggle: () => void;
}) {
  const failed = isFailedAction(action);

  return (
    <div className="flex gap-0">
      {/* Timeline line + dot */}
      <div className="flex flex-col items-center flex-shrink-0 w-10">
        <div
          className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0 ${
            failed
              ? 'bg-red-100 text-red-700 ring-2 ring-red-200'
              : action.status?.toUpperCase() === 'SUCCESS'
                ? 'bg-green-100 text-green-700'
                : 'bg-blue-100 text-blue-700'
          }`}
        >
          {failed ? (
            <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2.5}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          ) : (
            stepNumber
          )}
        </div>
        <div className="w-px flex-1 bg-gray-200 my-1" />
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0 pb-4">
        <div className="border border-gray-200 rounded-lg p-4 bg-white hover:border-gray-300 transition-colors">
          {/* Header row */}
          <div className="flex items-start justify-between gap-3 mb-2">
            <div className="flex items-center gap-2 flex-wrap min-w-0">
              <span className="text-sm font-semibold text-gray-900">
                {action.actionType}
              </span>
              <StatusBadge status={action.status} />
              {failed && action.errorCode && (
                <span className="px-2 py-0.5 text-xs font-mono font-medium bg-red-50 text-red-700 border border-red-200 rounded">
                  {action.errorCode}
                </span>
              )}
            </div>
            <button
              onClick={onToggle}
              className={`flex-shrink-0 px-3 py-1 text-xs font-medium rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors ${
                isSelected
                  ? 'bg-gray-100 text-gray-700 border-gray-300'
                  : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50'
              }`}
            >
              {isSelected ? 'Hide Details' : 'View Details'}
            </button>
          </div>

          {/* Service + Duration + Time + FlowId */}
          <div className="flex items-center gap-x-4 gap-y-1 text-xs text-gray-500 flex-wrap mb-2">
            {buildTimelineMetaItems(action).reduce<React.ReactNode[]>((acc, item, idx, arr) => {
              acc.push(item);
              if (idx < arr.length - 1) {
                acc.push(<span key={`sep-${idx}`} className="text-gray-300">·</span>);
              }
              return acc;
            }, [])}
          </div>

          {/* Message */}
          {action.message && (
            <p className={`text-sm mb-3 ${failed ? 'text-red-700' : 'text-gray-600'}`}>
              {action.message}
            </p>
          )}

          {/* Error message */}
          {failed && action.errorMessage && (
            <p className="text-xs text-red-600 bg-red-50 rounded px-3 py-2 border border-red-100 mb-3">
              {action.errorMessage}
            </p>
          )}

          {/* Link to original request-level detail */}
          <div className="flex items-center gap-3 mb-3">
            <Link
              to={`/logs/${action.flowId}`}
              className="text-xs text-blue-600 hover:text-blue-800 font-medium"
              target="_blank"
              rel="noopener noreferrer"
            >
              View Request Detail
            </Link>
          </div>

          {/* Inline detail panel */}
          {isSelected && (
            <div className="mt-3 pt-3 border-t border-gray-200">
              <ActionPayloadsPanel action={action} />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────────────────

export function BusinessFlowDetailPage() {
  const { orderCode } = useParams<{ orderCode: string }>();

  const [flow, setFlow] = useState<BusinessFlowDetail | null>(null);
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null);

  const [isLoadingFlow, setIsLoadingFlow] = useState(true);
  const [flowError, setFlowError] = useState<string | null>(null);
  const [flowNotFound, setFlowNotFound] = useState(false);

  const loadFlow = useCallback(() => {
    if (!orderCode) return;
    setIsLoadingFlow(true);
    setFlowError(null);
    setFlowNotFound(false);
    setFlow(null);

    getBusinessFlowByOrderCode(decodeURIComponent(orderCode))
      .then((data) => {
        setFlow(data);
      })
      .catch((error) => {
        if (error.response?.status === 404) {
          setFlowNotFound(true);
        } else {
          setFlowError('Unable to load business flow.');
        }
      })
      .finally(() => setIsLoadingFlow(false));
  }, [orderCode]);

  useEffect(() => {
    loadFlow();
  }, [loadFlow]);

  const handleToggleAction = (eventId: string) => {
    setSelectedActionId((prev) => (prev === eventId ? null : eventId));
  };

  if (isLoadingFlow) {
    return (
      <div className="p-6">
        <LoadingSkeleton />
      </div>
    );
  }

  if (flowNotFound) {
    return (
      <ErrorCard
        title="Flow not found"
        message="No flow found for the requested order code."
        onRetry={loadFlow}
        backLink="/logs"
      />
    );
  }

  if (flowError || !flow) {
    return (
      <ErrorCard
        title="Failed to load flow"
        message={flowError ?? 'An unexpected error occurred.'}
        onRetry={loadFlow}
        backLink="/logs"
      />
    );
  }

  const { summary, timeline } = flow;

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Link
          to="/logs"
          className="flex-shrink-0 flex items-center text-sm text-gray-500 hover:text-gray-900 transition-colors"
        >
          <svg
            className="w-4 h-4 mr-1"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M15 19l-7-7 7-7"
            />
          </svg>
          Logs
        </Link>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-xl font-semibold text-gray-900">Flow Detail</h1>
            <StatusBadge status={summary.overallStatus} />
          </div>
          <p
            className="text-sm text-gray-500 font-mono mt-0.5 truncate max-w-xl"
            title={summary.orderCode}
          >
            {summary.orderCode}
          </p>
        </div>
      </div>

      {/* What happened? Summary */}
      {((): React.ReactElement | null => {
        const overallStatus = summary.overallStatus?.toUpperCase();
        const servicesCount = summary.services?.length ?? 0;
        const actionCount = summary.actionCount ?? timeline.length;
        const failedActions = timeline.filter((a) => isFailedAction(a));
        let text = '';
        let bgClass = '';
        let borderClass = '';
        let textClass = '';

        if (overallStatus === 'SUCCESS') {
          text = `This flow completed successfully across ${servicesCount} service${servicesCount !== 1 ? 's' : ''} and ${actionCount} action${actionCount !== 1 ? 's' : ''}.`;
          bgClass = 'bg-green-50';
          borderClass = 'border-green-200';
          textClass = 'text-green-800';
        } else if (overallStatus === 'FAILED' || overallStatus === 'PARTIAL_FAILED') {
          const failedAction = failedActions.length > 0 ? failedActions[failedActions.length - 1] : null;
          if (failedAction) {
            text = `This flow failed at ${failedAction.serviceName}: ${failedAction.errorMessage || failedAction.message || 'Unknown error'}`;
          } else {
            text = `This flow contains ${actionCount} action${actionCount !== 1 ? 's' : ''} across ${servicesCount} service${servicesCount !== 1 ? 's' : ''}.`;
          }
          bgClass = overallStatus === 'FAILED' ? 'bg-red-50' : 'bg-amber-50';
          borderClass = overallStatus === 'FAILED' ? 'border-red-200' : 'border-amber-200';
          textClass = overallStatus === 'FAILED' ? 'text-red-800' : 'text-amber-800';
        } else {
          text = `This flow contains ${actionCount} action${actionCount !== 1 ? 's' : ''} across ${servicesCount} service${servicesCount !== 1 ? 's' : ''}.`;
          bgClass = 'bg-gray-50';
          borderClass = 'border-gray-200';
          textClass = 'text-gray-700';
        }

        return (
          <div className={`rounded-lg border px-4 py-3 ${bgClass} ${borderClass}`}>
            <p className={`text-sm font-medium ${textClass}`}>{text}</p>
          </div>
        );
      })()}

      {/* Business Summary Card */}
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
            Business Summary
          </h2>
        </div>
        <div className="p-6">
          {/* Contact + Identity row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <FieldRow
              label="User Email"
              value={summary.userEmail ?? EMPTY}
            />
            <FieldRow
              label="Customer Email"
              value={summary.customerEmail ?? EMPTY}
            />
            <FieldRow
              label="Customer Phone"
              value={summary.customerPhone ?? EMPTY}
            />
            <FieldRow
              label="Partner ID"
              value={summary.partnerId ?? EMPTY}
              isMonospace
            />
          </div>

          {/* Order identifiers row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <FieldRow label="Order Code" value={summary.orderCode} isMonospace />
            <FieldRow label="Payment ID" value={summary.paymentId ?? EMPTY} isMonospace />
            <FieldRow label="Transaction ID" value={summary.transactionId ?? EMPTY} isMonospace />
            <FieldRow label="Services Involved" value={summary.services.join(', ') || EMPTY} />
          </div>

          {/* Metrics row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <MetricPill label="Total Actions" value={summary.actionCount} />
            <MetricPill label="Success" value={summary.successCount} />
            <MetricPill label="Failed" value={summary.failedCount} />
            <MetricPill label="Status" value={summary.overallStatus.replace(/_/g, ' ')} />
          </div>

          {/* Last Message */}
          {summary.lastMessage && (
            <div className="mb-6 pb-6 border-b border-gray-100">
              <FieldRow label="Last Message" value={summary.lastMessage} />
            </div>
          )}

          {/* Timestamps row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
            <FieldRow label="First Seen" value={formatDate(summary.firstSeenAt)} />
            <FieldRow label="Last Seen" value={formatDate(summary.lastSeenAt)} />
            <FieldRow label="Last Service" value={summary.lastServiceName ?? EMPTY} />
            <FieldRow label="Last Action" value={summary.lastActionType ?? EMPTY} />
          </div>
        </div>
      </div>

      {/* Timeline Section */}
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
            Action Timeline ({timeline.length} actions across all requests)
          </h2>
        </div>
        <div className="p-6">
          {timeline.length === 0 ? (
            <div className="py-8 text-center text-sm text-gray-500">
              No actions found for this business flow.
            </div>
          ) : (
            <div className="space-y-0">
              {timeline.map((action, index) => (
                <BusinessActionTimelineItem
                  key={`${action.flowId}-${action.eventId}`}
                  action={action}
                  stepNumber={index + 1}
                  isSelected={selectedActionId === action.eventId}
                  onToggle={() => handleToggleAction(action.eventId)}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
