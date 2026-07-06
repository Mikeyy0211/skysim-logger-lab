export interface LogAction {
  actionId: string;
  flowId: string;
  serviceName: string;
  actionType: string;
  status: string;
  message: string | null;
  durationMs: number | null;
  createdAt: string | null;
  finishedAt: string | null;
  id?: string | null;
  eventId?: string | null;
  stepOrder?: number | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  requestTime?: string | null;
  responseTime?: string | null;
  correlationId?: string | null;
}

export interface LogActionDetailsResponse {
  action: LogAction;
  requestPayload?: string | null;
  responsePayload?: string | null;
  errorPayload?: string | null;
  metadata?: string | null;
}
