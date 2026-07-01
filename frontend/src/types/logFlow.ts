export type FlowStatus = 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED';

export interface LogFlowSummary {
  flowId: string;
  flowType: string;
  checkoutType: string | null;
  status: FlowStatus;
  customerEmail: string | null;
  customerPhone: string | null;
  userId: string | null;
  orderId: string | null;
  paymentId: string | null;
  totalSteps: number;
  successSteps: number;
  failedSteps: number;
  lastActionType: string | null;
  lastMessage: string | null;
  lastServiceName: string | null;
  startedAt: string;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export type LogFlowListResponse = PagedResponse<LogFlowSummary> | LogFlowSummary[];

export interface LogFlowDetail extends LogFlowSummary {
  // LogFlowDetail uses the same fields as LogFlowSummary
  // The backend returns the full flow details via GET /api/log-flows/{flowId}
}
