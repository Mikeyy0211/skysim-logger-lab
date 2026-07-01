import { useState, useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { MetricCard } from '../components/MetricCard';
import { EmptyState } from '../components/EmptyState';
import { getLogFlowById, getLogFlowActions } from '../services/logFlowService';
import { getLogActionDetails } from '../services/logFlowService';
import type { LogFlowDetail } from '../types/logFlow';
import type { LogAction, LogActionDetail } from '../types/logAction';

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

function safeStringify(payload: unknown): string {
  try {
    return JSON.stringify(payload, null, 2);
  } catch {
    return '—';
  }
}

export function LogDetailPage() {
  const { flowId } = useParams<{ flowId: string }>();

  const [flow, setFlow] = useState<LogFlowDetail | null>(null);
  const [actions, setActions] = useState<LogAction[]>([]);
  const [selectedActionDetails, setSelectedActionDetails] = useState<LogActionDetail[] | null>(null);
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null);

  const [isLoadingFlow, setIsLoadingFlow] = useState(true);
  const [isLoadingActions, setIsLoadingActions] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  const [flowError, setFlowError] = useState<string | null>(null);
  const [flowNotFound, setFlowNotFound] = useState(false);
  const [actionsError, setActionsError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);

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
        <div className="animate-pulse">
          <div className="h-4 w-32 bg-gray-200 rounded mb-4"></div>
          <div className="h-8 w-64 bg-gray-200 rounded mb-6"></div>
          <div className="h-64 bg-gray-200 rounded"></div>
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
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
          {flowError ?? 'Unable to load flow detail.'}
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
              <p className="text-sm font-medium text-gray-900">{flow.flowId}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Flow Type</p>
              <p className="text-sm font-medium text-gray-900">{flow.flowType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Checkout Type</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.checkoutType)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Status</p>
              <StatusBadge status={flow.status} />
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
              <p className="text-sm text-gray-500">Started At</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(flow.startedAt)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Completed At</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(flow.completedAt)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Updated At</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(flow.updatedAt)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Order ID</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.orderId)}</p>
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
              <p className="text-sm text-gray-500">Payment ID</p>
              <p className="text-sm font-medium text-gray-900">{formatFieldValue(flow.paymentId)}</p>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <MetricCard title="Total Steps" value={flow.totalSteps} />
        <MetricCard title="Success Steps" value={flow.successSteps} />
        <MetricCard title="Failed Steps" value={flow.failedSteps} />
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
                  <div className="w-3 h-3 bg-gray-200 rounded-full"></div>
                  <div className="flex-1">
                    <div className="h-4 bg-gray-200 rounded w-1/4 mb-2"></div>
                    <div className="h-3 bg-gray-200 rounded w-3/4"></div>
                  </div>
                </div>
              ))}
            </div>
          ) : actionsError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700 text-sm">
              {actionsError}
            </div>
          ) : actions.length === 0 ? (
            <p className="text-sm text-gray-500">No actions found for this flow.</p>
          ) : (
            <div className="space-y-4">
              {actions.map((action, index) => (
                <div key={action.actionId} className="flex gap-4">
                  <div className="flex flex-col items-center">
                    <div className="w-3 h-3 rounded-full bg-blue-600"></div>
                    {index < actions.length - 1 && (
                      <div className="w-0.5 h-full bg-gray-200 mt-1"></div>
                    )}
                  </div>
                  <div className="flex-1 pb-4">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-sm font-medium text-gray-900">{action.actionType}</span>
                      <StatusBadge status={action.status} />
                    </div>
                    <p className="text-sm text-gray-600 mb-1">{formatFieldValue(action.message)}</p>
                    <div className="flex items-center gap-4 text-xs text-gray-500">
                      <span>{action.serviceName}</span>
                      <span>•</span>
                      <span>{formatDuration(action.durationMs)}</span>
                      <span>•</span>
                      <span>{formatDate(action.createdAt)}</span>
                      <span>•</span>
                      <button
                        onClick={() => handleViewDetails(action.actionId)}
                        className="text-blue-600 hover:text-blue-800 font-medium"
                      >
                        {selectedActionId === action.actionId ? 'Hide Details' : 'View Details'}
                      </button>
                    </div>
                    {selectedActionId === action.actionId && (
                      <div className="mt-3">
                        {isLoadingDetails ? (
                          <div className="animate-pulse bg-gray-50 rounded p-4">
                            <div className="h-4 bg-gray-200 rounded w-1/3 mb-2"></div>
                            <div className="h-3 bg-gray-200 rounded w-full"></div>
                          </div>
                        ) : detailsError ? (
                          <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-red-700 text-sm">
                            {detailsError}
                          </div>
                        ) : selectedActionDetails && selectedActionDetails.length > 0 ? (
                          <div className="bg-gray-50 rounded-lg p-4">
                            {selectedActionDetails.map((detail) => (
                              <div key={detail.detailType} className="mb-3 last:mb-0">
                                <h4 className="text-xs font-medium text-gray-700 mb-1">
                                  {detail.detailType}
                                  {detail.masked && (
                                    <span className="ml-2 text-gray-400">(masked)</span>
                                  )}
                                </h4>
                                <pre className="text-xs text-gray-600 overflow-x-auto bg-white p-2 rounded border border-gray-200">
                                  {safeStringify(detail.payload)}
                                </pre>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <div className="text-sm text-gray-500 italic">
                            No details available for this action.
                          </div>
                        )}
                      </div>
                    )}
                  </div>
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
