import { apiClient } from './api';
import type { LogFlowListResponse, LogFlowSummary } from '../types/logFlow';

export interface LogFlowListParams {
  page?: number;
  pageSize?: number;
}

export async function getLogFlows(params?: LogFlowListParams): Promise<LogFlowSummary[]> {
  try {
    const response = await apiClient.get<LogFlowListResponse>('/api/log-flows', { params });
    const data = response.data;

    if (Array.isArray(data)) {
      return data;
    }

    if (data && typeof data === 'object' && 'items' in data) {
      return data.items;
    }

    return [];
  } catch (error) {
    console.error('Failed to fetch log flows:', error);
    throw error;
  }
}
