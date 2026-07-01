import { apiClient } from './api';
import type { LogFlowListResponse, LogFlowSummary, LogFlowDetail } from '../types/logFlow';
import type { LogAction, LogActionDetail } from '../types/logAction';

export interface LogFlowListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  flowType?: string;
  checkoutType?: string;
}

export async function getLogFlows(params?: LogFlowListParams): Promise<LogFlowSummary[]> {
  const response = await apiClient.get<LogFlowListResponse>('/api/log-flows', { params });
  const data = response.data;

  if (Array.isArray(data)) {
    return data;
  }

  if (data && typeof data === 'object' && 'items' in data) {
    return data.items;
  }

  return [];
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

export async function getLogActionDetails(actionId: string): Promise<LogActionDetail[]> {
  const response = await apiClient.get<LogActionDetail[] | LogActionDetail>(
    `/api/log-actions/${actionId}/details`
  );
  const data = response.data;

  if (Array.isArray(data)) {
    return data;
  }

  if (data && typeof data === 'object' && 'detailType' in data) {
    return [data];
  }

  return [];
}
