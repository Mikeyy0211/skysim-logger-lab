export type FlowStatus = 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED';

export interface LogFlowSummary {
  flowId: string;
  flowType: string;
  checkoutType: string | null;
  status: FlowStatus;
  userId: string | null;
  userEmail: string | null;
  username: string | null;
  partnerId: string | null;
  customerEmail: string | null;
  customerPhone: string | null;
  orderId: string | null;
  orderCode: string | null;
  paymentId: string | null;
  transactionId: string | null;
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
  // LogFlowDetail uses all fields from LogFlowSummary (inherited)
  // Additional timeline data comes from the API response
}

export interface DashboardMetrics {
  totalFlows: number;
  successFlows: number;
  failedFlows: number;
  runningFlows: number;
  partialFailed: number;
  averageDurationMs: number | null;
}
