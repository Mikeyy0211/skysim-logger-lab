import { useEffect, useRef, useState } from 'react';
import type { ChangeEvent, KeyboardEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { BusinessLogTable } from '../components/BusinessLogTable';
import { getLogFlows } from '../services/logFlowService';
import { getBusinessFlows } from '../services/businessFlowService';
import type { BusinessFlowSummary, LogFlowSummary } from '../types/logFlow';
import { useAppDispatch, useAppSelector } from '../app/hooks';
import {
  setKeyword,
  setStatus,
  setFromDate,
  setToDate,
  setPage,
  resetFilters,
  hydrateFiltersFromUrl,
} from '../features/logs/logFilterSlice';

type LogMode = 'business' | 'technical';
const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? EMPTY : date.toLocaleString();
}

function formatDuration(value: number | null | undefined): string {
  if (value == null || value < 0) return EMPTY;
  return value >= 1000 ? `${(value / 1000).toFixed(1)}s` : `${value}ms`;
}

function resolveTechnicalMessage(flow: LogFlowSummary): string {
  return flow.lastMessage?.trim() || '—';
}

function resolveTechnicalOrder(flow: LogFlowSummary): string {
  return flow.orderCode?.trim() || flow.orderId?.trim() || flow.paymentId?.trim() || EMPTY;
}

function TechnicalLogTable({ flows }: { flows: LogFlowSummary[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[980px]">
        <thead>
          <tr className="bg-gray-50">
            {['Thời gian', 'FlowId', 'Đơn hàng / Khóa', 'Dịch vụ', 'Hành động', 'Trạng thái', 'Thông báo', 'Thời gian xử lý', 'Thao tác'].map((label) => (
              <th key={label} className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {flows.map((flow) => {
            const message = resolveTechnicalMessage(flow);
            return (
              <tr key={flow.flowId} className="hover:bg-gray-50 align-top">
                <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-500">{formatDate(flow.updatedAt)}</td>
                <td className="px-4 py-4 max-w-[150px] text-xs font-mono text-gray-600"><span className="truncate block" title={flow.flowId}>{flow.flowId}</span></td>
                <td className="px-4 py-4 max-w-[180px] text-sm font-mono text-gray-600"><span className="truncate block" title={resolveTechnicalOrder(flow)}>{resolveTechnicalOrder(flow)}</span></td>
                <td className="px-4 py-4 text-sm text-gray-600">{flow.lastServiceName || EMPTY}</td>
                <td className="px-4 py-4 text-sm text-gray-600">{flow.lastActionType || EMPTY}</td>
                <td className="px-4 py-4 whitespace-nowrap"><StatusBadge status={flow.status} /></td>
                <td className="px-4 py-4 max-w-[260px] text-sm text-gray-600"><span className="truncate block" title={message}>{message}</span></td>
                <td className="px-4 py-4 whitespace-nowrap text-sm font-mono text-gray-600">{formatDuration(flow.durationMs)}</td>
                <td className="px-4 py-4 whitespace-nowrap text-sm">
                  <Link to={`/logs/${encodeURIComponent(flow.flowId)}`} className="text-blue-600 hover:text-blue-800 font-medium">
                    Xem chi tiết
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function LoadingState({ label }: { label: string }) {
  return (
    <div className="flex flex-col items-center justify-center p-12">
      <svg className="animate-spin w-8 h-8 text-blue-600 mb-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
      </svg>
      <p className="text-sm text-gray-500">{label}</p>
    </div>
  );
}

function EmptyState({ mode, onReset }: { mode: LogMode; onReset: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <p className="text-base font-medium text-gray-700 mb-1">
        {mode === 'business' ? 'Không tìm thấy đơn hàng phù hợp.' : 'Không tìm thấy nhật ký kỹ thuật phù hợp.'}
      </p>
      <p className="text-sm text-gray-500 text-center max-w-sm">
        Hãy thử điều chỉnh từ khóa, trạng thái hoặc khoảng thời gian.
      </p>
      <button type="button" onClick={onReset} className="mt-4 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700">
        Xóa bộ lọc
      </button>
    </div>
  );
}

export function LogListPage() {
  const dispatch = useAppDispatch();
  const [searchParams] = useSearchParams();
  const urlKeyword = searchParams.get('keyword') ?? '';
  const filters = useAppSelector((state) => state.logFilters);
  const [mode, setMode] = useState<LogMode>(() => searchParams.get('tab') === 'technical' ? 'technical' : 'business');
  const [draftKeyword, setDraftKeyword] = useState(() => urlKeyword || filters.keyword);
  const [filtersReady, setFiltersReady] = useState(!urlKeyword);
  const filtersInitialized = useRef(false);
  const [businessFlows, setBusinessFlows] = useState<BusinessFlowSummary[]>([]);
  const [technicalFlows, setTechnicalFlows] = useState<LogFlowSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    if (filtersInitialized.current) return;
    filtersInitialized.current = true;
    if (urlKeyword) dispatch(hydrateFiltersFromUrl({ keyword: urlKeyword }));
    setFiltersReady(true);
  }, [dispatch, urlKeyword]);

  async function fetchLogs(currentPage: number) {
    setIsLoading(true);
    setError(false);
    try {
      if (mode === 'business') {
        const response = await getBusinessFlows({
          keyword: filters.keyword.trim() || undefined,
          status: filters.status || undefined,
          fromDate: filters.fromDate || undefined,
          toDate: filters.toDate || undefined,
          page: currentPage,
          pageSize: filters.pageSize,
          sortBy: 'lastSeenAt',
          sortDirection: 'desc',
        });
        setBusinessFlows(response.items);
        setTotalPages(response.totalPages || 1);
      } else {
        const response = await getLogFlows({
          search: filters.keyword.trim() || undefined,
          status: filters.status || undefined,
          fromDate: filters.fromDate || undefined,
          toDate: filters.toDate || undefined,
          page: currentPage,
          pageSize: filters.pageSize,
        });
        setTechnicalFlows(response.items);
        setTotalPages(response.totalPages || 1);
      }
    } catch {
      setError(true);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    if (!filtersReady) return;
    void fetchLogs(filters.page);
    // fetchLogs intentionally reads the applied Redux filters for the active mode.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtersReady, filters.keyword, filters.status, filters.fromDate, filters.toDate, filters.page, filters.pageSize, mode]);

  function applyKeyword() {
    dispatch(setKeyword(draftKeyword));
  }

  function handleSearchKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') applyKeyword();
  }

  function handleReset() {
    setDraftKeyword('');
    dispatch(resetFilters());
    const url = new URL(window.location.href);
    url.searchParams.delete('keyword');
    window.history.replaceState(null, '', url.pathname + url.search);
  }

  function handlePrevious() {
    if (filters.page > 1) dispatch(setPage(filters.page - 1));
  }

  function handleNext() {
    if (filters.page < totalPages) dispatch(setPage(filters.page + 1));
  }

  const rows = mode === 'business' ? businessFlows : technicalFlows;

  return (
    <div className="p-6">
      <PageHeader title="Nhật ký" subtitle="Bắt đầu từ hành trình đơn hàng; mở dữ liệu kỹ thuật khi cần." />

      <div className="mb-6 inline-flex rounded-lg border border-gray-200 bg-white p-1 shadow-sm" role="tablist" aria-label="Chế độ xem nhật ký">
        <button type="button" role="tab" aria-selected={mode === 'business'} onClick={() => setMode('business')} className={`px-4 py-2 text-sm font-medium rounded-md ${mode === 'business' ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}>
          Nhật ký nghiệp vụ
        </button>
        <button type="button" role="tab" aria-selected={mode === 'technical'} onClick={() => setMode('technical')} className={`px-4 py-2 text-sm font-medium rounded-md ${mode === 'technical' ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}>
          Nhật ký kỹ thuật
        </button>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
        <div className="p-4 border-b border-gray-200">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="lg:col-span-2">
              <label htmlFor="log-search" className="block text-sm font-medium text-gray-700 mb-1">Tìm kiếm</label>
              <input id="log-search" type="text" placeholder="Mã đơn hàng, thanh toán, giao dịch, email hoặc số điện thoại" className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm" value={draftKeyword} onChange={(event: ChangeEvent<HTMLInputElement>) => setDraftKeyword(event.target.value)} onKeyDown={handleSearchKeyDown} />
            </div>
            <div>
              <label htmlFor="log-status" className="block text-sm font-medium text-gray-700 mb-1">Trạng thái</label>
              <select id="log-status" className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm" value={filters.status} onChange={(event) => dispatch(setStatus(event.target.value))}>
                <option value="">Tất cả trạng thái</option>
                <option value="SUCCESS">Hoàn tất</option>
                <option value="FAILED">Thất bại</option>
                <option value="RUNNING">Đang xử lý</option>
                <option value="PARTIAL_FAILED">Cần kiểm tra</option>
              </select>
            </div>
            <div>
              <label htmlFor="log-from-date" className="block text-sm font-medium text-gray-700 mb-1">Từ ngày</label>
              <input id="log-from-date" type="date" className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm" value={filters.fromDate} onChange={(event) => dispatch(setFromDate(event.target.value))} />
            </div>
            <div>
              <label htmlFor="log-to-date" className="block text-sm font-medium text-gray-700 mb-1">Đến ngày</label>
              <input id="log-to-date" type="date" className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm" value={filters.toDate} onChange={(event) => dispatch(setToDate(event.target.value))} />
            </div>
          </div>
          <div className="mt-4 flex items-center gap-2">
            <button type="button" onClick={applyKeyword} className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700">Tìm kiếm</button>
            <button type="button" onClick={handleReset} className="px-4 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-200">Xóa bộ lọc</button>
          </div>
        </div>

        {isLoading ? <LoadingState label={mode === 'business' ? 'Đang tải đơn hàng…' : 'Đang tải nhật ký kỹ thuật…'} /> : error ? (
          <div className="p-6">
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
              <p className="font-medium">Không thể tải dữ liệu.</p>
              <p className="text-sm mt-1">Vui lòng thử lại.</p>
              <button type="button" onClick={() => void fetchLogs(filters.page)} className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200">Thử lại</button>
            </div>
          </div>
        ) : rows.length === 0 ? <EmptyState mode={mode} onReset={handleReset} /> : (
          <>
            {mode === 'business' ? <BusinessLogTable flows={businessFlows} /> : <TechnicalLogTable flows={technicalFlows} />}
            <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-between">
              <button type="button" onClick={handlePrevious} disabled={filters.page <= 1} className="px-4 py-2 bg-white border border-gray-300 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">Trước</button>
              <span className="text-sm text-gray-600">Trang {filters.page} / {totalPages}</span>
              <button type="button" onClick={handleNext} disabled={filters.page >= totalPages} className="px-4 py-2 bg-white border border-gray-300 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">Sau</button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
