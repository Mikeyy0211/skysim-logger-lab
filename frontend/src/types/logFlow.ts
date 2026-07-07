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
  durationMs: number | null;
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

export interface RecentFlowItem {
  flowId: string;
  status: string;
  userId: string | null;
  userEmail: string | null;
  username: string | null;
  customerEmail: string | null;
  partnerId: string | null;
  orderCode: string | null;
  orderId: string | null;
  paymentId: string | null;
  transactionId: string | null;
  lastServiceName: string | null;
  lastActionType: string | null;
  lastMessage: string | null;
  lastDurationMs: number | null;
  updatedAt: string;
  createdAt: string;
}

export interface DashboardMetrics {
  totalFlows: number;
  totalActions: number;
  logsToday: number;
  logsThisWeek: number;
  successFlows: number;
  failedFlows: number;
  runningFlows: number;
  partialFailed: number;
  successRate: number;
  averageDurationMs: number | null;
  recentFailedFlows: RecentFlowItem[];
  recentSuccessFlows: RecentFlowItem[];
}

// ─── Business Flow Types ──────────────────────────────────────────────────────

export interface BusinessFlowSummary {
  orderCode: string;
  representativeFlowId: string;
  userEmail: string | null;
  customerEmail: string | null;
  customerPhone: string | null;
  partnerId: string | null;
  paymentId: string | null;
  transactionId: string | null;
  overallStatus: string;
  services: string[];
  actionCount: number;
  failedCount: number;
  successCount: number;
  firstSeenAt: string;
  lastSeenAt: string;
  lastMessage: string | null;
  lastServiceName: string | null;
  lastActionType: string | null;
}

export interface BusinessFlowAction {
  flowId: string;
  eventId: string;
  serviceName: string;
  actionType: string;
  status: string;
  message: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  durationMs: number | null;
  correlationId: string | null;
  createdAt: string;
  requestTime: string | null;
  responseTime: string | null;
  requestPayload: string | null;
  responsePayload: string | null;
  errorPayload: string | null;
  metadata: string | null;
}

export interface BusinessFlowDetail {
  summary: BusinessFlowSummary;
  timeline: BusinessFlowAction[];
}
