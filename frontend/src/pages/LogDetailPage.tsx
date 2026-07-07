import React, { useState, useEffect, useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { StatusBadge } from '../components/StatusBadge';
import { CheckoutTypeBadge } from '../components/CheckoutTypeBadge';
import { EmptyState } from '../components/EmptyState';
import {
  getLogFlowById,
  getLogFlowActions,
  getLogActionDetails,
} from '../services/logFlowService';
import type { LogFlowDetail } from '../types/logFlow';
import type { LogAction, LogActionDetailsResponse } from '../types/logAction';

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

function formatFieldValue(value: string | null | undefined): string {
  return value ?? EMPTY;
}

function prettyJson(value: unknown): string {
  if (value === undefined) return '';
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
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

/** Parse duration from message text like "Request completed (380ms)" */
function parseDurationFromMessage(message: string | null | undefined): number | null {
  if (!message) return null;
  const match = message.match(/\((\d+)(?:ms|s)\)/);
  if (match) {
    const num = parseInt(match[1], 10);
    if (match[0].endsWith('s')) return num * 1000;
    return num;
  }
  return null;
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

// ─── Collapsible Section ────────────────────────────────────────────────────────

function CollapsibleSection({
  title,
  defaultOpen = false,
  children,
}: {
  title: string;
  defaultOpen?: boolean;
  children: React.ReactNode;
}) {
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <div>
      <button
        onClick={() => setIsOpen((prev) => !prev)}
        className="flex items-center gap-2 text-xs font-semibold text-gray-700 uppercase tracking-wide hover:text-gray-900 focus:outline-none focus:underline"
      >
        <span className={`transition-transform duration-150 ${isOpen ? 'rotate-90' : ''}`}>
          <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M9 5l7 7-7 7" />
          </svg>
        </span>
        {title}
      </button>
      {isOpen && <div className="mt-2">{children}</div>}
    </div>
  );
}

// ─── Compact Raw JSON Block ────────────────────────────────────────────────────

function CompactRawBlock({ value }: { value: unknown }) {
  return (
    <pre className="text-xs text-gray-800 bg-gray-50 p-3 rounded border border-gray-200 font-mono leading-relaxed max-w-full max-h-72 overflow-x-auto overflow-y-auto whitespace-pre-wrap break-words">
      {prettyJson(value)}
    </pre>
  );
}

// ─── Request Summary ────────────────────────────────────────────────────────────

function RequestSummaryView({ raw }: { raw: string | null | undefined }) {
  const [showFullHeaders, setShowFullHeaders] = useState(false);

  if (!raw) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <CompactRawBlock value={raw} />;
  }

  const method = parsed.method as string | undefined;
  const path = parsed.path as string | undefined;
  const fullUrl = parsed.fullUrl as string | undefined;
  const clientIp = parsed.clientIp as string | undefined;
  const contentType = parsed.contentType as string | undefined;

  const headersRaw =
    parsed.headers !== undefined
      ? parsed.headers
      : parsed.requestHeaders !== undefined
        ? parsed.requestHeaders
        : undefined;

  const bodyValue =
    parsed.body !== undefined
      ? parsed.body
      : parsed.requestBody !== undefined
        ? parsed.requestBody
        : parsed.requestPayload !== undefined
          ? parsed.requestPayload
          : undefined;

  const headers = isPlainObject(headersRaw) ? (headersRaw as Record<string, string>) : null;
  const hasAuthHeader = headers && Object.keys(headers).some((k) => k.toLowerCase() === 'authorization');

  // Shorten long URLs
  const displayUrl = (() => {
    if (!fullUrl) return null;
    if (fullUrl.length <= 100) return fullUrl;
    return fullUrl.substring(0, 100) + '…';
  })();

  // Truncate body preview
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
      {/* Method / Path / URL / Client IP */}
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
              <span
                className="text-gray-900 font-mono break-all min-w-0"
                title={fullUrl ?? undefined}
              >
                {displayUrl}
              </span>
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

      {/* Body preview */}
      {bodyPreview && (
        <div className="min-w-0">
          <p className="text-xs text-gray-500 mb-1">Body</p>
          <pre className="text-xs text-gray-800 bg-gray-50 p-2 rounded border border-gray-200 font-mono whitespace-pre-wrap break-words max-w-full overflow-x-auto">
            {bodyPreview}
          </pre>
        </div>
      )}

      {/* Full Headers toggle */}
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

// ─── Response Summary ───────────────────────────────────────────────────────────

function ResponseSummaryView({ raw }: { raw: string | null | undefined }) {
  if (!raw) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <CompactRawBlock value={raw} />;
  }

  const statusCode = parsed.statusCode as number | undefined;
  const durationMs = parsed.durationMs as number | undefined;

  const bodyValue =
    parsed.body !== undefined
      ? parsed.body
      : parsed.responseBody !== undefined
        ? parsed.responseBody
        : undefined;

  // Parse important business fields from response body
  let errorCode: string | number | undefined;
  let message: string | undefined;
  let orderCode: string | undefined;
  let paymentId: string | undefined;
  let transPaymentId: string | undefined;
  let transactionId: string | number | undefined;
  let paymentUrl: string | undefined;

  if (bodyValue) {
    const bodyParsed = parseJsonSafely(typeof bodyValue === 'string' ? bodyValue : prettyJson(bodyValue));
    if (isPlainObject(bodyParsed)) {
      const data = (bodyParsed as Record<string, unknown>).data as Record<string, unknown> | undefined;
      const payment = data && isPlainObject(data) ? (data as Record<string, unknown>).payment as Record<string, unknown> | undefined : undefined;

      errorCode = bodyParsed.errorCode as string | number | undefined;
      message = bodyParsed.message as string | undefined;
      orderCode = (bodyParsed.orderCode ?? bodyParsed.billOrder ?? bodyParsed.order_code ?? bodyParsed.bill_order) as string | undefined;
      paymentId = (bodyParsed.paymentId ?? bodyParsed.payment_id) as string | undefined;
      transPaymentId = (bodyParsed.transPaymentId ?? bodyParsed.trans_payment_id) as string | undefined;
      transactionId = (bodyParsed.transactionId ?? bodyParsed.transId ?? bodyParsed.transaction_id ?? bodyParsed.trans_id) as string | number | undefined;

      if (payment) {
        paymentId ??= (payment.paymentId ?? payment.payment_id) as string | undefined;
        transPaymentId ??= (payment.transPaymentId ?? payment.trans_payment_id) as string | undefined;
        transactionId ??= (payment.transactionId ?? payment.transId) as string | number | undefined;
        orderCode ??= (payment.billOrder ?? payment.bill_order) as string | undefined;
        paymentUrl = (payment.paymentUrl ?? payment.payment_url) as string | undefined;
      }
    }
  }

  // Truncate body preview
  const bodyPreview = (() => {
    if (!bodyValue) return null;
    const str = typeof bodyValue === 'string' ? bodyValue : prettyJson(bodyValue);
    if (str.length <= 400) return str;
    return str.substring(0, 400) + '…';
  })();

  const hasBusinessFields = errorCode !== undefined || message || orderCode || paymentId || transPaymentId || transactionId || paymentUrl;

  return (
    <div className="space-y-2 min-w-0">
      {/* Status + Duration */}
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

      {/* Business fields from body */}
      {hasBusinessFields && (
        <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-x-2 gap-y-1 text-xs items-baseline min-w-0">
          {errorCode !== undefined && (
            <>
              <span className="text-gray-500 truncate">errorCode</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{errorCode}</span>
            </>
          )}
          {message && (
            <>
              <span className="text-gray-500 truncate">message</span>
              <span className="text-gray-900 break-all min-w-0">{message}</span>
            </>
          )}
          {orderCode && (
            <>
              <span className="text-gray-500 truncate">orderCode</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{orderCode}</span>
            </>
          )}
          {paymentId && (
            <>
              <span className="text-gray-500 truncate">paymentId</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{paymentId}</span>
            </>
          )}
          {transPaymentId && (
            <>
              <span className="text-gray-500 truncate">transPaymentId</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{transPaymentId}</span>
            </>
          )}
          {transactionId !== undefined && (
            <>
              <span className="text-gray-500 truncate">transactionId</span>
              <span className="text-gray-900 font-mono break-all min-w-0">{transactionId}</span>
            </>
          )}
          {paymentUrl && (
            <>
              <span className="text-gray-500 truncate">paymentUrl</span>
              <span
                className="text-gray-900 font-mono break-all min-w-0"
                title={paymentUrl}
              >
                {paymentUrl.length > 80 ? paymentUrl.substring(0, 80) + '…' : paymentUrl}
              </span>
            </>
          )}
        </div>
      )}

      {/* Body preview */}
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

// ─── Metadata Summary ──────────────────────────────────────────────────────────

function MetadataSummaryView({ raw }: { raw: string | null | undefined }) {
  if (!raw) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <CompactRawBlock value={raw} />;
  }

  // Show important fields only
  const importantFields: Array<[string, unknown]> = [];
  for (const [k, v] of Object.entries(parsed)) {
    if (
      k === 'authResult' ||
      k === 'userEmail' ||
      k === 'partnerId' ||
      k === 'userId' ||
      k === 'hasAuthorization' ||
      k === 'isAuthenticated' ||
      k === 'correlationId' ||
      k === 'username'
    ) {
      const displayVal = typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean'
        ? String(v)
        : v === null
          ? 'null'
          : prettyJson(v);
      importantFields.push([k, displayVal]);
    }
  }

  if (importantFields.length === 0) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  return (
    <div className="space-y-1 min-w-0">
      {importantFields.map(([k, v]) => (
        <div
          key={k}
          className="grid grid-cols-[8rem_minmax(0,1fr)] gap-x-2 text-xs items-baseline min-w-0"
        >
          <span className="text-gray-500 truncate">{k}</span>
          <span className="text-gray-900 font-mono break-all min-w-0">{String(v)}</span>
        </div>
      ))}
    </div>
  );
}

// ─── Action Detail Panel ────────────────────────────────────────────────────────

function ActionPayloadsPanel({
  details,
  isLoading,
  error,
  onRetry,
}: {
  details: LogActionDetailsResponse | null;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  if (isLoading) {
    return (
      <div className="animate-pulse space-y-3">
        <div className="h-3 bg-gray-200 rounded w-1/4" />
        <div className="h-24 bg-gray-200 rounded" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded p-3">
        <p className="text-red-700 text-xs font-medium">Failed to load details</p>
        <p className="text-red-600 text-xs mt-1">{error}</p>
        <button
          onClick={onRetry}
          className="mt-2 px-3 py-1 bg-red-100 text-red-700 text-xs font-medium rounded hover:bg-red-200"
        >
          Retry
        </button>
      </div>
    );
  }

  if (!details) {
    return <p className="text-xs text-gray-400 italic">—</p>;
  }

  // Compute action duration: prefer action.durationMs, fallback to parsing message
  const actionDuration = details.action.durationMs != null
    ? details.action.durationMs
    : parseDurationFromMessage(details.action.message);

  return (
    <div className="space-y-4">
      {/* Action Overview — always open by default */}
      <div>
        <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
          Action Overview
        </h4>
        <div className="bg-white rounded border border-gray-200 p-3 max-w-full overflow-hidden">
          <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-x-2 gap-y-1 text-xs items-baseline min-w-0">
            <span className="text-gray-500 truncate">Service</span>
            <span className="text-gray-900 break-all min-w-0">{details.action.serviceName}</span>

            <span className="text-gray-500 truncate">Action</span>
            <span className="text-gray-900 break-all min-w-0">{details.action.actionType}</span>

            <span className="text-gray-500 truncate">Status</span>
            <span className="text-gray-900 break-all min-w-0">{details.action.status}</span>

            <span className="text-gray-500 truncate">Duration</span>
            <span className="text-gray-900 break-all min-w-0">{formatDuration(actionDuration)}</span>

            {details.action.message && (
              <>
                <span className="text-gray-500 truncate">Message</span>
                <span className="text-gray-900 break-all min-w-0">{details.action.message}</span>
              </>
            )}
            {details.action.errorCode && (
              <>
                <span className="text-gray-500 truncate">Error Code</span>
                <span className="text-gray-900 break-all min-w-0">{details.action.errorCode}</span>
              </>
            )}
            {details.action.errorMessage && (
              <>
                <span className="text-gray-500 truncate">Error Message</span>
                <span className="text-gray-900 break-all min-w-0">{details.action.errorMessage}</span>
              </>
            )}
            <span className="text-gray-500 truncate">Started</span>
            <span className="text-gray-900 break-all min-w-0">{formatDate(details.action.createdAt)}</span>
            <span className="text-gray-500 truncate">Finished</span>
            <span className="text-gray-900 break-all min-w-0">{formatDate(details.action.finishedAt ?? null)}</span>
          </div>
        </div>
      </div>

      {/* Request Summary — always open */}
      {details.requestPayload && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Request
          </h4>
          <RequestSummaryView raw={details.requestPayload} />
        </div>
      )}

      {/* Response Summary — always open */}
      {details.responsePayload && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Response
          </h4>
          <ResponseSummaryView raw={details.responsePayload} />
        </div>
      )}

      {/* Error — open if present */}
      {details.errorPayload && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Error
          </h4>
          <CompactRawBlock
            value={parseJsonSafely(details.errorPayload) ?? details.errorPayload}
          />
        </div>
      )}

      {/* Metadata Summary — always open */}
      {details.metadata && (
        <div>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
            Metadata
          </h4>
          <MetadataSummaryView raw={details.metadata} />
        </div>
      )}

      {/* Raw Request — collapsed by default */}
      {details.requestPayload && (
        <CollapsibleSection title="Raw Request" defaultOpen={false}>
          <CompactRawBlock value={parseJsonSafely(details.requestPayload) ?? details.requestPayload} />
        </CollapsibleSection>
      )}

      {/* Raw Response — collapsed by default */}
      {details.responsePayload && (
        <CollapsibleSection title="Raw Response" defaultOpen={false}>
          <CompactRawBlock value={parseJsonSafely(details.responsePayload) ?? details.responsePayload} />
        </CollapsibleSection>
      )}

      {/* Raw Metadata — collapsed by default */}
      {details.metadata && (
        <CollapsibleSection title="Raw Metadata" defaultOpen={false}>
          <CompactRawBlock value={parseJsonSafely(details.metadata) ?? details.metadata} />
        </CollapsibleSection>
      )}
    </div>
  );
}

// ─── Timeline Item ──────────────────────────────────────────────────────────────

function isFailedAction(action: LogAction): boolean {
  const s = action.status?.toUpperCase();
  return s === 'FAILED' || s === 'ERROR' || s === 'TIMEOUT';
}

/**
 * Build timeline metadata items array for a LogAction.
 * Each entry is either a JSX element or null (filtered out before render).
 * Separators " · " are added between non-null items at render time.
 */
function buildTimelineMetaItems(action: LogAction): React.ReactNode[] {
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

  return items;
}

function ActionTimelineItem({
  action,
  stepNumber,
  isSelected,
  isLoadingDetails,
  details,
  detailsError,
  onToggle,
  onRetry,
}: {
  action: LogAction;
  stepNumber: number;
  isSelected: boolean;
  isLoadingDetails: boolean;
  details: LogActionDetailsResponse | null;
  detailsError: string | null;
  onToggle: () => void;
  onRetry: () => void;
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

          {/* Service + Duration + Time */}
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

          {/* Inline detail panel */}
          {isSelected && (
            <div className="mt-3 pt-3 border-t border-gray-200">
              <ActionPayloadsPanel
                details={details}
                isLoading={isLoadingDetails}
                error={detailsError}
                onRetry={onRetry}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────────────────

export function LogDetailPage() {
  const { flowId } = useParams<{ flowId: string }>();

  const [flow, setFlow] = useState<LogFlowDetail | null>(null);
  const [actions, setActions] = useState<LogAction[]>([]);
  const [selectedActionDetails, setSelectedActionDetails] =
    useState<LogActionDetailsResponse | null>(null);
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null);

  const [isLoadingFlow, setIsLoadingFlow] = useState(true);
  const [isLoadingActions, setIsLoadingActions] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  const [flowError, setFlowError] = useState<string | null>(null);
  const [flowNotFound, setFlowNotFound] = useState(false);
  const [actionsError, setActionsError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);

  const loadFlow = useCallback(() => {
    if (!flowId) return;
    setIsLoadingFlow(true);
    setFlowError(null);
    setFlowNotFound(false);
    setFlow(null);

    getLogFlowById(flowId)
      .then((data) => {
        setFlow(data);
      })
      .catch((error) => {
        if (error.response?.status === 404) {
          setFlowNotFound(true);
        } else {
          setFlowError('Unable to load flow detail.');
        }
      })
      .finally(() => setIsLoadingFlow(false));
  }, [flowId]);

  const loadActions = useCallback(() => {
    if (!flowId) return;
    setIsLoadingActions(true);
    setActionsError(null);
    setActions([]);

    getLogFlowActions(flowId)
      .then((data) => {
        setActions(Array.isArray(data) ? data : []);
      })
      .catch(() => {
        setActionsError('Unable to load actions.');
      })
      .finally(() => setIsLoadingActions(false));
  }, [flowId]);

  useEffect(() => {
    loadFlow();
    loadActions();
  }, [loadFlow, loadActions]);

  const loadActionDetails = useCallback(
    (actionId: string) => {
      setSelectedActionId(actionId);
      setSelectedActionDetails(null);
      setDetailsError(null);
      setIsLoadingDetails(true);

      getLogActionDetails(actionId)
        .then((data) => {
          setSelectedActionDetails(data);
        })
        .catch(() => {
          setDetailsError('Unable to load action details.');
        })
        .finally(() => setIsLoadingDetails(false));
    },
    [],
  );

  const handleToggleAction = (actionId: string) => {
    if (selectedActionId === actionId) {
      setSelectedActionId(null);
      setSelectedActionDetails(null);
      setDetailsError(null);
      setIsLoadingDetails(false);
    } else {
      loadActionDetails(actionId);
    }
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
        message="The requested flow does not exist or has been removed."
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

  // Duration: use flow.durationMs from API (computed from startedAt/completedAt).
  // No Date.now() trick — no "(running)" for SUCCESS/FAILED.
  const isFlowRunning = flow.status === 'RUNNING';
  const flowDurationDisplay = flow.durationMs != null
    ? formatDuration(flow.durationMs)
    : isFlowRunning
      ? formatDuration(Math.floor((Date.now() - new Date(flow.startedAt).getTime())))
      : EMPTY;

  const displaySubtitle = flow.orderCode ?? flow.orderId ?? flow.paymentId ?? flow.userId ?? flow.flowId;

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
            <StatusBadge status={flow.status} />
          </div>
          <p
            className="text-sm text-gray-500 font-mono mt-0.5 truncate max-w-xl"
            title={displaySubtitle}
          >
            {displaySubtitle}
          </p>
        </div>
      </div>

      {/* What happened? Summary */}
      {((): React.ReactElement | null => {
        const overallStatus = flow.status?.toUpperCase();
        const distinctServices = [...new Set(actions.map((a) => a.serviceName).filter(Boolean))];
        const servicesCount = distinctServices.length;
        const actionCount = actions.length;
        const failedActions = actions.filter((a) => isFailedAction(a));
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

      {/* Business Summary Card — most prominent */}
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
            Business Summary
          </h2>
        </div>
        <div className="p-6">
          {/* User / Email / Partner / Customer Phone row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <FieldRow
              label="User Email"
              value={formatFieldValue(flow.userEmail)}
            />
            <FieldRow
              label="Customer Email"
              value={formatFieldValue(flow.customerEmail)}
            />
            <FieldRow label="Partner ID" value={formatFieldValue(flow.partnerId)} />
            <FieldRow
              label="Customer Phone"
              value={formatFieldValue(flow.customerPhone)}
            />
          </div>

          {/* Order identifiers row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <FieldRow label="Order Code" value={formatFieldValue(flow.orderCode)} isMonospace />
            <FieldRow label="Order ID" value={formatFieldValue(flow.orderId)} isMonospace />
            <FieldRow label="Payment ID" value={formatFieldValue(flow.paymentId)} isMonospace />
            <FieldRow label="Transaction ID" value={formatFieldValue(flow.transactionId)} isMonospace />
          </div>

          {/* Status + Service + Action + Duration row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-6 pb-6 border-b border-gray-100">
            <MetricPill label="Status" value={flow.status.replace(/_/g, ' ')} />
            <MetricPill label="Last Service" value={formatFieldValue(flow.lastServiceName)} />
            <MetricPill label="Last Action" value={formatFieldValue(flow.lastActionType)} />
            <MetricPill label="Duration" value={flowDurationDisplay} />
          </div>

          {/* Last Message */}
          {flow.lastMessage && (
            <div className="mb-6 pb-6 border-b border-gray-100">
              <FieldRow label="Last Message" value={flow.lastMessage} />
            </div>
          )}

          {/* Timestamps row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
            <FieldRow label="Started At" value={formatDate(flow.startedAt)} />
            <FieldRow label="Completed At" value={formatDate(flow.completedAt)} />
            <FieldRow label="Created At" value={formatDate(flow.createdAt)} />
            <FieldRow label="Updated At" value={formatDate(flow.updatedAt)} />
          </div>
        </div>
      </div>

      {/* Technical Details Card */}
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
            Technical Details
          </h2>
        </div>
        <div className="p-6">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
            <FieldRow label="Flow ID" value={flow.flowId} isMonospace />
            <FieldRow label="Flow Type" value={formatFieldValue(flow.flowType)} />
            <FieldRow
              label="Checkout Type"
              value={
                flow.checkoutType ? (
                  <span className="inline-flex items-center">
                    <CheckoutTypeBadge checkoutType={flow.checkoutType} />
                  </span>
                ) : (
                  EMPTY
                )
              }
            />
            <FieldRow label="User ID" value={formatFieldValue(flow.userId)} isMonospace />
          </div>

          {/* Action counts */}
          <div className="grid grid-cols-3 gap-6 mt-6 pt-6 border-t border-gray-100">
            <div className="flex flex-col gap-1">
              <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
                Total Steps
              </span>
              <span className="text-lg font-bold text-gray-900">{flow.totalSteps}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
                Success Steps
              </span>
              <span className="text-lg font-bold text-green-600">{flow.successSteps}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
                Failed Steps
              </span>
              <span className="text-lg font-bold text-red-600">{flow.failedSteps}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Timeline Section */}
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
          <h2 className="text-sm font-semibold text-gray-900 uppercase tracking-wide">
            Action Timeline
          </h2>
        </div>
        <div className="p-6">
          {isLoadingActions ? (
            <div className="animate-pulse space-y-4">
              {[1, 2, 3].map((i) => (
                <div key={i} className="flex gap-4">
                  <div className="w-10 flex-shrink-0 flex flex-col items-center">
                    <div className="w-6 h-6 bg-gray-200 rounded-full" />
                    <div className="w-px flex-1 bg-gray-200 mt-1" />
                  </div>
                  <div className="flex-1 space-y-2">
                    <div className="h-4 bg-gray-200 rounded w-1/3" />
                    <div className="h-3 bg-gray-200 rounded w-1/2" />
                    <div className="h-3 bg-gray-200 rounded w-2/3" />
                  </div>
                </div>
              ))}
            </div>
          ) : actionsError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <p className="text-red-700 text-sm font-medium">Failed to load actions</p>
              <p className="text-red-600 text-sm mt-1">{actionsError}</p>
              <button
                onClick={loadActions}
                className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200"
              >
                Retry
              </button>
            </div>
          ) : actions.length === 0 ? (
            <EmptyState message="No actions found for this flow." />
          ) : (
            <div className="space-y-0">
              {actions.map((action, index) => (
                <ActionTimelineItem
                  key={action.actionId}
                  action={action}
                  stepNumber={index + 1}
                  isSelected={selectedActionId === action.actionId}
                  isLoadingDetails={isLoadingDetails && selectedActionId === action.actionId}
                  details={
                    selectedActionId === action.actionId ? selectedActionDetails : null
                  }
                  detailsError={
                    selectedActionId === action.actionId ? detailsError : null
                  }
                  onToggle={() => handleToggleAction(action.actionId)}
                  onRetry={() => loadActionDetails(action.actionId)}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
