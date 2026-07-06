import { useState, useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { CheckoutTypeBadge } from '../components/CheckoutTypeBadge';
import { MetricCard } from '../components/MetricCard';
import { EmptyState } from '../components/EmptyState';
import { getLogFlowById, getLogFlowActions, getLogActionDetails } from '../services/logFlowService';
import type { LogFlowDetail } from '../types/logFlow';
import type { LogAction, LogActionDetailsResponse } from '../types/logAction';

function formatFieldValue(value: string | null | undefined): string {
  return value ?? '—';
}

function formatDuration(ms: number | null): string {
  if (ms === null || ms === undefined) return '—';
  if (ms >= 1000) {
    return `${(ms / 1000).toFixed(1)}s`;
  }
  return `${ms}ms`;
}

function formatDate(dateString: string | null | undefined): string {
  if (!dateString) return '—';
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return '—';
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
  if (typeof value !== 'string') return value;
  if (value.length === 0) return value;
  return value.replace(/(Bearer\s+)[^\s"']+/gi, '$1***');
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function prettyJson(value: unknown): string {
  if (value === undefined) return '';
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function PrettyJsonBlock({ value, emptyText = 'No data' }: { value: unknown; emptyText?: string }) {
  if (value === null || value === undefined || value === '') {
    return <p className="text-xs text-gray-400 italic">{emptyText}</p>;
  }

  return (
    <pre className="text-xs text-gray-800 overflow-x-auto bg-white p-3 rounded border border-gray-200 font-mono leading-relaxed max-h-64 overflow-y-auto whitespace-pre-wrap break-words">
      {prettyJson(value)}
    </pre>
  );
}

function KeyValueRow({ label, value }: { label: string; value: string | number | boolean | null | undefined }) {
  const display = value === null || value === undefined || value === '' ? '—' : String(value);

  return (
    <div className="flex items-baseline gap-2 text-xs py-1">
      <span className="text-gray-500 w-32 flex-shrink-0">{label}</span>
      <span className="text-gray-900 font-mono break-all">{display}</span>
    </div>
  );
}

function RequestPayloadView({ raw }: { raw: string | null | undefined }) {
  if (raw === null || raw === undefined || raw === '') {
    return <p className="text-xs text-gray-400 italic">No request captured.</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <PrettyJsonBlock value={raw} />;
  }

  // Support both backend shapes:
  // - canonical: { method, path, fullUrl, clientIp, query, headers, body }
  // - legacy:    { method, path, fullUrl, clientIp, query, requestHeaders, requestBody, requestPayload }
  const method = parsed.method;
  const path = parsed.path;
  const fullUrl = parsed.fullUrl;
  const clientIp = parsed.clientIp;
  const query = parsed.query ?? parsed.queryString;

  const headersValue =
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

  const hasStructuredShape =
    method !== undefined ||
    path !== undefined ||
    fullUrl !== undefined ||
    clientIp !== undefined ||
    query !== undefined ||
    headersValue !== undefined ||
    bodyValue !== undefined;

  if (!hasStructuredShape) {
    return <PrettyJsonBlock value={parsed} />;
  }

  const headers = isPlainObject(headersValue) ? (headersValue as Record<string, string>) : null;
  const maskedHeaders = headers
    ? Object.fromEntries(
        Object.entries(headers).map(([k, v]) => {
          const key = k.toLowerCase();
          if (key === 'authorization') {
            return [k, maskAuthorization(typeof v === 'string' ? v : String(v))];
          }
          return [k, typeof v === 'string' ? v : prettyJson(v)];
        }),
      )
    : null;

  return (
    <div className="space-y-3">
      <div className="bg-white rounded border border-gray-200 p-3">
        <KeyValueRow label="Method" value={(method as string | undefined) ?? null} />
        <KeyValueRow label="Path" value={(path as string | undefined) ?? null} />
        <KeyValueRow label="Full URL" value={(fullUrl as string | undefined) ?? null} />
        <KeyValueRow label="Client IP" value={(clientIp as string | undefined) ?? null} />
        {query !== undefined && (
          <div className="py-1">
            <p className="text-xs text-gray-500 mb-1">Query</p>
            <pre className="text-xs text-gray-800 bg-gray-50 rounded p-2 border border-gray-200 font-mono whitespace-pre-wrap break-words">
              {prettyJson(query)}
            </pre>
          </div>
        )}
      </div>

      <div>
        <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-1">Headers</p>
        {maskedHeaders ? (
          <div className="bg-white rounded border border-gray-200 p-3">
            {Object.entries(maskedHeaders).map(([k, v]) => (
              <KeyValueRow key={k} label={k} value={v} />
            ))}
          </div>
        ) : (
          <p className="text-xs text-gray-400 italic">No headers captured.</p>
        )}
      </div>

      <div>
        <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-1">Body</p>
        <PrettyJsonBlock value={bodyValue ?? null} emptyText="No body captured." />
      </div>
    </div>
  );
}

function ResponsePayloadView({ raw }: { raw: string | null | undefined }) {
  if (raw === null || raw === undefined || raw === '') {
    return <p className="text-xs text-gray-400 italic">No response captured.</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <PrettyJsonBlock value={raw} />;
  }

  // Support both backend shapes:
  // - canonical: { statusCode, headers, body, durationMs }
  // - legacy:    { responseHeaders, responseBody }
  const statusCode = parsed.statusCode;
  const durationMs = parsed.durationMs;

  const headersValue =
    parsed.headers !== undefined
      ? parsed.headers
      : parsed.responseHeaders !== undefined
        ? parsed.responseHeaders
        : undefined;

  const bodyValue =
    parsed.body !== undefined
      ? parsed.body
      : parsed.responseBody !== undefined
        ? parsed.responseBody
        : undefined;

  const hasStructuredShape =
    statusCode !== undefined ||
    durationMs !== undefined ||
    headersValue !== undefined ||
    bodyValue !== undefined;

  if (!hasStructuredShape) {
    return <PrettyJsonBlock value={parsed} />;
  }

  const headers = isPlainObject(headersValue) ? (headersValue as Record<string, string>) : null;
  const maskedHeaders = headers
    ? Object.fromEntries(
        Object.entries(headers).map(([k, v]) => {
          const key = k.toLowerCase();
          if (key === 'authorization' || key === 'set-cookie') {
            return [k, '***'];
          }
          return [k, typeof v === 'string' ? v : prettyJson(v)];
        }),
      )
    : null;

  return (
    <div className="space-y-3">
      <div className="bg-white rounded border border-gray-200 p-3">
        <KeyValueRow label="Status Code" value={(statusCode as number | string | undefined) ?? null} />
        <KeyValueRow label="Duration" value={durationMs !== undefined ? `${durationMs}ms` : null} />
      </div>

      <div>
        <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-1">Headers</p>
        {maskedHeaders ? (
          <div className="bg-white rounded border border-gray-200 p-3">
            {Object.entries(maskedHeaders).map(([k, v]) => (
              <KeyValueRow key={k} label={k} value={v} />
            ))}
          </div>
        ) : (
          <p className="text-xs text-gray-400 italic">No headers captured.</p>
        )}
      </div>

      <div>
        <p className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-1">Body</p>
        <PrettyJsonBlock value={bodyValue ?? null} emptyText="No body captured." />
      </div>
    </div>
  );
}

function MetadataPayloadView({ raw }: { raw: string | null | undefined }) {
  if (raw === null || raw === undefined || raw === '') {
    return <p className="text-xs text-gray-400 italic">No metadata captured.</p>;
  }

  const parsed = parseJsonSafely(raw);

  if (!isPlainObject(parsed)) {
    return <PrettyJsonBlock value={raw} />;
  }

  const identity: Array<[string, unknown]> = [];
  const rest: Record<string, unknown> = {};

  for (const [k, v] of Object.entries(parsed)) {
    if (k === 'userId' || k === 'userEmail' || k === 'username' || k === 'roles' || k === 'authResult') {
      identity.push([k, v]);
    } else {
      rest[k] = v;
    }
  }

  return (
    <div className="space-y-3">
      {identity.length > 0 && (
        <div className="bg-blue-50 border border-blue-200 rounded p-3">
          <p className="text-xs font-semibold text-blue-900 uppercase tracking-wide mb-1">Identity</p>
          {identity.map(([k, v]) => (
            <KeyValueRow
              key={k}
              label={k}
              value={
                typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean'
                  ? (v as string | number | boolean)
                  : prettyJson(v)
              }
            />
          ))}
        </div>
      )}

      <div>
        {Object.keys(rest).length === 0 ? (
          <p className="text-xs text-gray-400 italic">No additional metadata.</p>
        ) : (
          <PrettyJsonBlock value={rest} />
        )}
      </div>
    </div>
  );
}

export function LogDetailPage() {
  const { flowId } = useParams<{ flowId: string }>();

  const [flow, setFlow] = useState<LogFlowDetail | null>(null);
  const [actions, setActions] = useState<LogAction[]>([]);
  const [selectedActionDetails, setSelectedActionDetails] = useState<LogActionDetailsResponse | null>(null);
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null);

  const [isLoadingFlow, setIsLoadingFlow] = useState(true);
  const [isLoadingActions, setIsLoadingActions] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  const [flowError, setFlowError] = useState<string | null>(null);
  const [flowNotFound, setFlowNotFound] = useState(false);
  const [actionsError, setActionsError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);

  function retryFlow() {
    if (!flowId) return;
    setIsLoadingFlow(true);
    setFlowError(null);
    setFlowNotFound(false);
    setFlow(null);
    getLogFlowById(flowId)
      .then((data) => {
        setFlow(data);
        setIsLoadingFlow(false);
      })
      .catch((error) => {
        if (error.response?.status === 404) {
          setFlowNotFound(true);
        } else {
          setFlowError('Unable to load flow detail.');
        }
        setIsLoadingFlow(false);
      });
  }

  function retryActions() {
    if (!flowId) return;
    setIsLoadingActions(true);
    setActionsError(null);
    setActions([]);
    getLogFlowActions(flowId)
      .then((data) => {
        setActions(Array.isArray(data) ? data : []);
        setIsLoadingActions(false);
      })
      .catch(() => {
        setActionsError('Unable to load actions.');
        setActions([]);
        setIsLoadingActions(false);
      });
  }

  useEffect(() => {
    if (!flowId) return;

    let cancelled = false;
    setIsLoadingFlow(true);
    setFlowError(null);
    setFlowNotFound(false);
    setFlow(null);

    getLogFlowById(flowId)
      .then((data) => {
        if (!cancelled) {
          setFlow(data);
          setIsLoadingFlow(false);
        }
      })
      .catch((error) => {
        if (!cancelled) {
          if (error.response?.status === 404) {
            setFlowNotFound(true);
          } else {
            setFlowError('Unable to load flow detail.');
          }
          setIsLoadingFlow(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [flowId]);

  useEffect(() => {
    if (!flowId) return;

    let cancelled = false;
    setIsLoadingActions(true);
    setActionsError(null);
    setActions([]);

    getLogFlowActions(flowId)
      .then((data) => {
        if (!cancelled) {
          setActions(Array.isArray(data) ? data : []);
          setIsLoadingActions(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setActionsError('Unable to load actions.');
          setActions([]);
          setIsLoadingActions(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [flowId]);

  const handleViewDetails = async (actionId: string) => {
    if (selectedActionId === actionId) {
      setSelectedActionId(null);
      setSelectedActionDetails(null);
      return;
    }

    setSelectedActionId(actionId);
    setSelectedActionDetails(null);
    setDetailsError(null);
    setIsLoadingDetails(true);

    try {
      const details = await getLogActionDetails(actionId);
      setSelectedActionDetails(details);
    } catch {
      setDetailsError('Unable to load action details.');
    } finally {
      setIsLoadingDetails(false);
    }
  };

  if (isLoadingFlow) {
    return (
      <div className="p-6">
        <div className="animate-pulse space-y-6">
          <div className="h-4 w-24 bg-gray-200 rounded"></div>
          <div>
            <div className="h-8 w-48 bg-gray-200 rounded mb-2"></div>
            <div className="h-4 w-32 bg-gray-200 rounded"></div>
          </div>
          <div className="bg-white rounded-lg border border-gray-200 p-6">
            <div className="h-6 w-40 bg-gray-200 rounded mb-6"></div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {[...Array(6)].map((_, i) => (
                <div key={i}>
                  <div className="h-3 w-24 bg-gray-200 rounded mb-2"></div>
                  <div className="h-4 w-40 bg-gray-200 rounded"></div>
                </div>
              ))}
            </div>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="bg-white rounded-lg border border-gray-200 p-4">
                <div className="h-3 w-24 bg-gray-200 rounded mb-2"></div>
                <div className="h-8 w-12 bg-gray-200 rounded"></div>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (flowNotFound) {
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

  if (flowError || !flow) {
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
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <p className="text-red-700 font-medium">Failed to load flow detail</p>
          <p className="text-red-600 text-sm mt-1">{flowError ?? 'An unexpected error occurred.'}</p>
          <button
            onClick={retryFlow}
            className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
          >
            Retry
          </button>
        </div>
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
              <p className="text-sm font-mono font-medium text-gray-900 break-all">{flow.flowId}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Status</p>
              <div className="mt-1">
                <StatusBadge status={flow.status} />
              </div>
            </div>
            <div>
              <p className="text-sm text-gray-500">Checkout Type</p>
              <div className="mt-1">
                <CheckoutTypeBadge checkoutType={flow.checkoutType} />
              </div>
            </div>
            <div>
              <p className="text-sm text-gray-500">Flow Type</p>
              <p className="text-sm font-medium text-gray-900">{flow.flowType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Last Action</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.lastActionType)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Last Message</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.lastMessage)}</p>
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
              <p className="text-sm text-gray-500">Created At</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(flow.createdAt)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Updated At</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(flow.updatedAt)}</p>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-6">
        <MetricCard title="Total Steps" value={flow.totalSteps} />
        <MetricCard title="Success Steps" value={flow.successSteps} />
        <MetricCard title="Failed Steps" value={flow.failedSteps} />
        <MetricCard title="Last Service" value={formatFieldValue(flow.lastServiceName)} />
        <MetricCard title="Last Action" value={formatFieldValue(flow.lastActionType)} />
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm mb-6">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Action Timeline</h2>
        </div>
        <div className="p-6">
          {isLoadingActions ? (
            <div className="animate-pulse space-y-4">
              {[1, 2, 3].map((i) => (
                <div key={i} className="flex gap-4">
                  <div className="w-3 h-3 bg-gray-200 rounded-full mt-1.5 flex-shrink-0"></div>
                  <div className="flex-1 space-y-2">
                    <div className="h-4 bg-gray-200 rounded w-1/4"></div>
                    <div className="h-3 bg-gray-200 rounded w-3/4"></div>
                    <div className="h-3 bg-gray-200 rounded w-1/2"></div>
                  </div>
                </div>
              ))}
            </div>
          ) : actionsError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <p className="text-red-700 text-sm font-medium">Failed to load actions</p>
              <p className="text-red-600 text-sm mt-1">{actionsError}</p>
              <button
                onClick={retryActions}
                className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
              >
                Retry
              </button>
            </div>
          ) : actions.length === 0 ? (
            <p className="text-sm text-gray-500 italic">No actions found for this flow.</p>
          ) : (
            <div className="space-y-3">
              {actions.map((action, index) => (
                <div
                  key={action.actionId}
                  className="border border-gray-200 rounded-lg p-4 hover:border-gray-300 transition-colors"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-start gap-3 flex-1 min-w-0">
                      <div className="flex flex-col items-center mt-1 flex-shrink-0">
                        <div className={`w-3 h-3 rounded-full ${index === 0 ? 'bg-blue-600' : 'bg-gray-300'}`}></div>
                        {index < actions.length - 1 && (
                          <div className="w-0.5 flex-1 bg-gray-200 mt-1" style={{ minHeight: '12px' }}></div>
                        )}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          <span className="text-sm font-semibold text-gray-900">{action.actionType}</span>
                          <StatusBadge status={action.status} />
                        </div>
                        {action.message && (
                          <p className="text-sm text-gray-600 mb-2">{action.message}</p>
                        )}
                        <div className="flex items-center gap-x-4 gap-y-1 text-xs text-gray-500 flex-wrap">
                          <span className="font-medium text-gray-600">{action.serviceName}</span>
                          <span className="text-gray-300">|</span>
                          <span>{formatDuration(action.durationMs)}</span>
                          <span className="text-gray-300">|</span>
                          <span>{formatDate(action.createdAt)}</span>
                        </div>
                      </div>
                    </div>
                    <div className="flex-shrink-0">
                      <button
                        onClick={() => handleViewDetails(action.actionId)}
                        className="px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 hover:border-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
                      >
                        {selectedActionId === action.actionId ? 'Hide Details' : 'View Details'}
                      </button>
                    </div>
                  </div>

                  {selectedActionId === action.actionId && (
                    <div className="mt-4 ml-6">
                      {isLoadingDetails ? (
                        <div className="animate-pulse bg-gray-50 rounded-lg p-4 space-y-2">
                          <div className="h-3 bg-gray-200 rounded w-1/4"></div>
                          <div className="h-3 bg-gray-200 rounded w-full"></div>
                        </div>
                      ) : detailsError ? (
                        <div className="bg-red-50 border border-red-200 rounded-lg p-3">
                          <p className="text-red-700 text-sm font-medium">Failed to load details</p>
                          <p className="text-red-600 text-sm mt-1">{detailsError}</p>
                          <button
                            onClick={() => handleViewDetails(action.actionId)}
                            className="mt-2 px-3 py-1 bg-red-100 text-red-700 text-xs font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
                          >
                            Retry
                          </button>
                        </div>
                      ) : selectedActionDetails ? (
                        <div className="space-y-5">
                            <section>
                              <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
                                Action Overview
                              </h4>
                              <div className="bg-white rounded border border-gray-200 p-3 grid grid-cols-1 md:grid-cols-2 gap-x-4 gap-y-2 text-xs">
                                <KeyValueRow label="Action Type" value={selectedActionDetails.action.actionType} />
                                <div>
                                  <span className="text-gray-500">Status: </span>
                                  <span className="text-gray-900">{selectedActionDetails.action.status}</span>
                                </div>
                                <KeyValueRow label="Service" value={selectedActionDetails.action.serviceName} />
                                <KeyValueRow label="Message" value={selectedActionDetails.action.message ?? null} />
                                <KeyValueRow label="Duration" value={formatDuration(selectedActionDetails.action.durationMs)} />
                                <KeyValueRow label="Started" value={formatDate(selectedActionDetails.action.createdAt)} />
                                <KeyValueRow label="Finished" value={formatDate(selectedActionDetails.action.finishedAt)} />
                                {selectedActionDetails.action.errorCode && (
                                  <KeyValueRow label="Error Code" value={selectedActionDetails.action.errorCode} />
                                )}
                                {selectedActionDetails.action.errorMessage && (
                                  <div className="md:col-span-2">
                                    <span className="text-gray-500">Error Message: </span>
                                    <span className="text-gray-900 break-words">{selectedActionDetails.action.errorMessage}</span>
                                  </div>
                                )}
                              </div>
                            </section>

                            <section>
                              <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
                                Request
                              </h4>
                              <RequestPayloadView raw={selectedActionDetails.requestPayload ?? null} />
                            </section>

                            <section>
                              <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
                                Response
                              </h4>
                              <ResponsePayloadView raw={selectedActionDetails.responsePayload ?? null} />
                            </section>

                            <section>
                              <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
                                Error
                              </h4>
                              {selectedActionDetails.errorPayload === null ||
                              selectedActionDetails.errorPayload === undefined ||
                              selectedActionDetails.errorPayload === '' ? (
                                <p className="text-xs text-gray-400 italic">No error captured.</p>
                              ) : (
                                <PrettyJsonBlock
                                  value={parseJsonSafely(selectedActionDetails.errorPayload) ?? selectedActionDetails.errorPayload}
                                />
                              )}
                            </section>

                            <section>
                              <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-2">
                                Metadata
                              </h4>
                              <MetadataPayloadView raw={selectedActionDetails.metadata ?? null} />
                            </section>
                          </div>
                      ) : (
                        <p className="text-sm text-gray-400 italic">No details available for this action.</p>
                      )}
                    </div>
                  )}
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
          <div className="text-sm text-gray-500">
            <p>Click "View Details" on any action above to see technical payloads.</p>
            <p className="mt-2">Sensitive fields are masked by the backend.</p>
          </div>
        </div>
      </div>
    </div>
  );
}
