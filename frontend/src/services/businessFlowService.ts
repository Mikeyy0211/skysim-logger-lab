import { apiClient } from './api';
import type { BusinessFlowSummary, BusinessFlowDetail, PagedResponse } from '../types/logFlow';
import type { LogActionDetailsResponse } from '../types/logAction';

export interface BusinessFlowListParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export async function getBusinessFlows(
  params?: BusinessFlowListParams
): Promise<PagedResponse<BusinessFlowSummary>> {
  const response = await apiClient.get<PagedResponse<BusinessFlowSummary> | BusinessFlowSummary[]>(
    '/api/business-flows',
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
    const paged = data as PagedResponse<BusinessFlowSummary>;
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

export async function getBusinessFlowByOrderCode(orderCode: string): Promise<BusinessFlowDetail> {
  const response = await apiClient.get<BusinessFlowDetail>(`/api/business-flows/${encodeURIComponent(orderCode)}`);
  return response.data;
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
