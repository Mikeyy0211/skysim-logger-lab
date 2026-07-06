import { apiClient } from './api';
import type {
  LogFlowSummary,
  LogFlowDetail,
  PagedResponse,
  DashboardMetrics,
} from '../types/logFlow';
import type { LogAction, LogActionDetailsResponse } from '../types/logAction';

export interface LogFlowListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  flowType?: string;
  checkoutType?: string;
  fromDate?: string;
  toDate?: string;
}

export async function getLogFlows(params?: LogFlowListParams): Promise<PagedResponse<LogFlowSummary>> {
  const response = await apiClient.get<PagedResponse<LogFlowSummary> | LogFlowSummary[]>(
    '/api/log-flows',
    { params }
  );
  const data = response.data;

  if (Array.isArray(data)) {
    return {
      items: data,
      page: 1,
      pageSize: data.length,
      totalItems: data.length,
      totalPages: 1,
    };
  }

  if (data && typeof data === 'object' && 'items' in data) {
    const paged = data as PagedResponse<LogFlowSummary>;
    return {
      items: Array.isArray(paged.items) ? paged.items : [],
      page: paged.page ?? 1,
      pageSize: paged.pageSize ?? (Array.isArray(paged.items) ? paged.items.length : 0),
      totalItems: paged.totalItems ?? (Array.isArray(paged.items) ? paged.items.length : 0),
      totalPages: paged.totalPages ?? 1,
    };
  }

  return {
    items: [],
    page: 1,
    pageSize: 0,
    totalItems: 0,
    totalPages: 1,
  };
}

export async function getLogFlowById(flowId: string): Promise<LogFlowDetail> {
  const response = await apiClient.get<unknown>(`/api/log-flows/${flowId}`);
  const data = response.data;

  if (data && typeof data === 'object' && 'flow' in data) {
    const envelope = data as { flow?: LogFlowDetail };
    if (!envelope.flow) {
      throw new Error('Flow not found');
    }

    return envelope.flow;
  }

  return data as LogFlowDetail;
}

export async function getDashboardMetrics(): Promise<DashboardMetrics> {
  const response = await apiClient.get<DashboardMetrics>('/api/dashboard/metrics');
  return response.data;
}

export function normalizeLogAction(raw: unknown): LogAction {
  const record = (raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {}) as Record<string, unknown>;

  return {
    actionId: (record.actionId ?? record.id ?? '') as string,
    flowId: (record.flowId ?? '') as string,
    serviceName: (record.serviceName ?? '—') as string,
    actionType: (record.actionType ?? '—') as string,
    status: (record.status ?? record.actionStatus ?? 'UNKNOWN') as string,
    message: (record.message ?? record.errorMessage ?? '—') as string | null,
    durationMs: (record.durationMs ?? null) as number | null,
    createdAt: (record.createdAt ?? record.requestTime ?? '') as string,
    finishedAt: (record.finishedAt ?? record.responseTime ?? null) as string | null,
  };
}

export async function getLogFlowActions(flowId: string): Promise<LogAction[]> {
  const response = await apiClient.get<unknown>(`/api/log-flows/${flowId}/actions`);
  const data = response.data;

  if (Array.isArray(data)) {
    return data.map(normalizeLogAction);
  }

  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>;

    if (Array.isArray(record.items)) {
      return (record.items as unknown[]).map(normalizeLogAction);
    }

    if (Array.isArray(record.data)) {
      return (record.data as unknown[]).map(normalizeLogAction);
    }

    if (Array.isArray(record.timeline)) {
      return (record.timeline as unknown[]).map(normalizeLogAction);
    }
  }

  return [];
}

export async function getLogActionDetails(actionId: string): Promise<LogActionDetailsResponse> {
  const response = await apiClient.get<LogActionDetailsResponse>(
    `/api/log-actions/${actionId}/details`
  );
  const data = response.data;

  if (data && typeof data === 'object' && 'action' in data) {
    return data;
  }

  const envelope = data as { data?: LogActionDetailsResponse } | null;
  if (envelope && envelope.data && typeof envelope.data === 'object' && 'action' in envelope.data) {
    return envelope.data;
  }

  throw new Error('Invalid action details response');
}
