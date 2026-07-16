import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { BusinessFlowJourney } from '../components/BusinessFlowJourney';
import { StatusBadge } from '../components/StatusBadge';
import { getBusinessFlowByOrderCode } from '../services/businessFlowService';
import type { BusinessFlowDetail } from '../types/logFlow';
import { buildBusinessIssue, buildWhatHappened } from '../features/logs/businessFlowSummary';
import { getCurrentStepDisplay } from '../features/logs/businessActionMapping';
import { getBusinessStatusLabel, isBusinessFailure } from '../features/logs/businessStatusDisplay';

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? EMPTY : date.toLocaleString();
}

function Field({ label, value, mono = false }: { label: string; value: string | number; mono?: boolean }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-gray-500 uppercase tracking-wide font-medium">{label}</dt>
      <dd className={`mt-1 text-sm text-gray-900 break-words ${mono ? 'font-mono' : ''}`}>{value || EMPTY}</dd>
    </div>
  );
}

function LoadingState() {
  return (
    <div className="p-6 space-y-6 animate-pulse">
      <div className="h-4 w-20 bg-gray-200 rounded" />
      <div className="h-8 w-64 bg-gray-200 rounded" />
      <div className="h-36 bg-white border border-gray-200 rounded-lg" />
      <div className="h-64 bg-white border border-gray-200 rounded-lg" />
    </div>
  );
}

function ErrorState({ notFound, onRetry }: { notFound: boolean; onRetry: () => void }) {
  return (
    <div className="p-6">
      <Link to="/logs" className="text-sm text-gray-600 hover:text-gray-900">← Quay lại Nhật ký</Link>
      <div className="mt-4 bg-red-50 border border-red-200 rounded-lg p-6">
        <p className="font-semibold text-red-700">{notFound ? 'Không tìm thấy đơn hàng' : 'Không thể tải hành trình đơn hàng'}</p>
        <p className="text-sm text-red-600 mt-1">{notFound ? 'Không tìm thấy hành trình nghiệp vụ cho mã đơn hàng này.' : 'Vui lòng thử lại.'}</p>
        <button type="button" onClick={onRetry} className="mt-4 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200">Thử lại</button>
      </div>
    </div>
  );
}

function isNotFoundError(error: unknown): boolean {
  if (!error || typeof error !== 'object' || !('response' in error)) return false;
  const response = (error as { response?: { status?: number } }).response;
  return response?.status === 404;
}

export function BusinessFlowDetailPage() {
  const { orderCode: encodedOrderCode } = useParams<{ orderCode: string }>();
  const [flow, setFlow] = useState<BusinessFlowDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [hasError, setHasError] = useState(false);

  const loadFlow = useCallback(() => {
    if (!encodedOrderCode) return;
    setIsLoading(true);
    setNotFound(false);
    setHasError(false);
    setFlow(null);

    void getBusinessFlowByOrderCode(decodeURIComponent(encodedOrderCode))
      .then(setFlow)
      .catch((error: unknown) => {
        if (isNotFoundError(error)) setNotFound(true);
        else setHasError(true);
      })
      .finally(() => setIsLoading(false));
  }, [encodedOrderCode]);

  useEffect(() => {
    loadFlow();
  }, [loadFlow]);

  if (isLoading) return <LoadingState />;
  if (notFound || hasError || !flow) return <ErrorState notFound={notFound} onRetry={loadFlow} />;

  const { summary, timeline } = flow;
  const whatHappened = buildWhatHappened(timeline);
  const issue = buildBusinessIssue(timeline);
  const currentAction = timeline[timeline.length - 1];
  const currentStep = currentAction
    ? getCurrentStepDisplay(currentAction.actionType, currentAction.message)
    : getCurrentStepDisplay(summary.lastActionType, summary.lastMessage);
  const flowIds = [...new Set(timeline.map((action) => action.flowId).filter(Boolean))];
  const requiresAttention = isBusinessFailure(summary.overallStatus);

  return (
    <div className="p-6 space-y-6">
      <header className="flex items-start gap-4">
        <Link to="/logs" className="flex-shrink-0 text-sm text-gray-500 hover:text-gray-900">← Nhật ký</Link>
        <div className="min-w-0">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-2xl font-semibold text-gray-900">Đơn hàng {summary.orderCode}</h1>
            <StatusBadge status={summary.overallStatus} friendly />
          </div>
          <p className="text-sm text-gray-500 mt-1">Hành trình được tổng hợp từ {summary.technicalFlowCount} luồng kỹ thuật</p>
        </div>
      </header>

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
        <h2 className="text-lg font-semibold text-gray-900">Điều gì đã xảy ra?</h2>
        <ul className="mt-4 space-y-2 text-sm text-gray-700">
          {whatHappened.map((sentence, index) => <li key={`${sentence}-${index}`} className="flex gap-2"><span className="text-blue-600">•</span><span>{sentence}</span></li>)}
        </ul>
      </section>

      {requiresAttention && (
        <section className="bg-red-50 border border-red-200 rounded-lg p-6">
          <h2 className="text-lg font-semibold text-red-800">Vấn đề cần kiểm tra</h2>
          {issue ? (
            <dl className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field label="Bước cần kiểm tra" value={issue.step} />
              <Field label="Dịch vụ" value={issue.service} />
              <Field label="Lỗi" value={issue.error} mono />
              <Field label="Thời gian" value={formatDate(issue.time)} />
              {issue.message && <Field label="Thông báo kỹ thuật" value={issue.message} />}
              {issue.suggestion && <Field label="Gợi ý kiểm tra" value={issue.suggestion} />}
            </dl>
          ) : (
            <p className="mt-3 text-sm text-red-700">Kiểm tra dữ liệu kỹ thuật của bước xảy ra lỗi.</p>
          )}
        </section>
      )}

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
        <h2 className="text-lg font-semibold text-gray-900">Thông tin đơn hàng</h2>
        <dl className="mt-5 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-5">
          <Field label="Mã đơn hàng" value={summary.orderCode} mono />
          <Field label="Mã thanh toán" value={summary.paymentId ?? EMPTY} mono />
          <Field label="Mã giao dịch" value={summary.transactionId ?? EMPTY} mono />
          <Field label="Bước gần nhất" value={currentStep} />
          <Field label="Trạng thái" value={getBusinessStatusLabel(summary.overallStatus)} />
        </dl>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
        <h2 className="text-lg font-semibold text-gray-900">Thông tin khách hàng</h2>
        <dl className="mt-5 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <Field label="Email người dùng" value={summary.userEmail ?? EMPTY} />
          <Field label="Email khách hàng" value={summary.customerEmail ?? EMPTY} />
          <Field label="Số điện thoại khách hàng" value={summary.customerPhone ?? EMPTY} />
          <Field label="Mã đối tác" value={summary.partnerId ?? EMPTY} mono />
          <Field label="Mã người dùng" value={summary.userId ?? EMPTY} mono />
        </dl>
      </section>

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
        <h2 className="text-lg font-semibold text-gray-900">Tổng hợp xử lý</h2>
        <dl className="mt-5 grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-5">
          <Field label="Bắt đầu" value={formatDate(summary.firstSeenAt)} />
          <Field label="Cập nhật gần nhất" value={formatDate(summary.lastSeenAt)} />
          <Field label="Tổng số bước" value={summary.actionCount} />
          <Field label="Bước thành công" value={summary.successCount} />
          <Field label="Bước thất bại" value={summary.failedCount} />
          <Field label="Luồng kỹ thuật" value={summary.technicalFlowCount} />
        </dl>
      </section>

      <BusinessFlowJourney actions={timeline} />

      <section className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        <details>
          <summary className="cursor-pointer px-6 py-4 text-sm font-semibold text-gray-900">Chi tiết kỹ thuật</summary>
          <div className="border-t border-gray-100 px-6 py-4 text-sm text-gray-600">
            <p>Dữ liệu kỹ thuật được thu gọn trong từng bước của hành trình.</p>
            <p className="mt-3 font-medium text-gray-700">FlowId kỹ thuật</p>
            <ul className="mt-1 space-y-1 font-mono text-xs text-gray-500">
              {flowIds.length > 0 ? flowIds.map((id) => <li key={id}>{id}</li>) : <li>{EMPTY}</li>}
            </ul>
          </div>
        </details>
      </section>
    </div>
  );
}
