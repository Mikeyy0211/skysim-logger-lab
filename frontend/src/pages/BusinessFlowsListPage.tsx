import { useEffect, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { UserCustomerCell } from '../components/UserCustomerCell';
import { getBusinessFlows } from '../services/businessFlowService';
import type { BusinessFlowSummary } from '../types/logFlow';

const PAGE_SIZE = 10;

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const d = new Date(value);
  if (isNaN(d.getTime())) return EMPTY;
  return d.toLocaleString();
}

function resolveDisplayPaymentId(bf: BusinessFlowSummary): string {
  return bf.paymentId?.trim() || EMPTY;
}

function resolveDisplayTransactionId(bf: BusinessFlowSummary): string {
  return bf.transactionId?.trim() || EMPTY;
}

function resolveDisplayLastMessage(bf: BusinessFlowSummary): string {
  return bf.lastMessage?.trim() || EMPTY;
}

function resolveDisplayServices(bf: BusinessFlowSummary): string {
  if (!bf.services || bf.services.length === 0) return EMPTY;
  return bf.services.join(', ');
}

export function BusinessFlowsListPage() {
  const [flows, setFlows] = useState<BusinessFlowSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  async function fetchFlows(currentPage: number) {
    try {
      setIsLoading(true);
      setError(null);

      const params: Record<string, unknown> = {
        page: currentPage,
        pageSize: PAGE_SIZE,
      };
      if (search.trim()) params.keyword = search.trim();

      const paged = await getBusinessFlows(params as Parameters<typeof getBusinessFlows>[0]);

      setFlows(paged.items);
      setPage(paged.page);
      setTotalPages(paged.totalPages);
    } catch {
      setError('Không thể tải danh sách luồng nghiệp vụ.');
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    fetchFlows(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function handleSearchKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') {
      setPage(1);
      fetchFlows(1);
    }
  }

  function handleSearchClick() {
    setPage(1);
    fetchFlows(1);
  }

  function handleReset() {
    setSearch('');
    setPage(1);
    fetchFlows(1);
  }

  function handlePrevious() {
    if (page > 1) {
      const newPage = page - 1;
      setPage(newPage);
      fetchFlows(newPage);
    }
  }

  function handleNext() {
    if (page < totalPages) {
      const newPage = page + 1;
      setPage(newPage);
      fetchFlows(newPage);
    }
  }

  return (
    <div className="p-6">
      <PageHeader
        title="Luồng nghiệp vụ"
        subtitle="Danh sách theo đơn hàng, tổng hợp từ các yêu cầu dịch vụ"
      />

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm mb-6">
        <div className="p-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="flex-1">
              <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
                Tìm kiếm
              </label>
              <input
                id="search"
                type="text"
                placeholder="Mã đơn hàng, mã thanh toán, mã giao dịch, email khách hàng hoặc số điện thoại..."
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={handleSearchKeyDown}
              />
            </div>
            <div className="flex items-end gap-2">
              <button
                type="button"
                onClick={handleSearchClick}
                className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
              >
                Tìm kiếm
              </button>
              <button
                type="button"
                onClick={handleReset}
                className="px-4 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-gray-400 transition-colors"
              >
                Đặt lại
              </button>
            </div>
          </div>
        </div>

        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center p-12">
              <svg className="animate-spin w-8 h-8 text-blue-600 mb-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <p className="text-sm text-gray-500">Đang tải luồng nghiệp vụ...</p>
            </div>
          ) : error ? (
            <div className="p-6">
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
                <p className="font-medium mb-1">Không thể tải luồng nghiệp vụ</p>
                <p className="text-sm">{error}</p>
                <button
                  onClick={() => fetchFlows(page)}
                  className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
                >
                  Thử lại
                </button>
              </div>
            </div>
          ) : flows.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 px-4">
              <svg
                className="w-12 h-12 mb-4 text-gray-300"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={1.5}
                  d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
                />
              </svg>
              <p className="text-base font-medium text-gray-700 mb-1">Không tìm thấy luồng nghiệp vụ</p>
              <p className="text-sm text-gray-500 text-center max-w-sm">
                Không có luồng có mã đơn hàng phù hợp. Hãy thử điều chỉnh tiêu chí tìm kiếm.
              </p>
            </div>
          ) : (
            <>
              <table className="w-full">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Cập nhật gần nhất
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Người dùng / Khách hàng
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Mã đơn hàng
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Mã thanh toán
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Mã giao dịch
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Dịch vụ
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Số bước
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Trạng thái
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Thông báo gần nhất
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {flows.map((bf) => {
                    const paymentId = resolveDisplayPaymentId(bf);
                    const transactionId = resolveDisplayTransactionId(bf);
                    const services = resolveDisplayServices(bf);
                    const lastMessage = resolveDisplayLastMessage(bf);

                    return (
                      <tr key={bf.orderCode} className="hover:bg-gray-50">
                        <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-500">
                          {formatDate(bf.lastSeenAt)}
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap max-w-[200px]">
                          <UserCustomerCell
                            userEmail={bf.userEmail}
                            customerEmail={bf.customerEmail}
                          />
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap max-w-[180px] text-sm text-gray-900 font-mono font-medium">
                          <span className="truncate block" title={bf.orderCode}>
                            {bf.orderCode}
                          </span>
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap max-w-[160px] text-sm text-gray-600">
                          <span className="truncate block font-mono" title={paymentId !== EMPTY ? paymentId : undefined}>
                            {paymentId}
                          </span>
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap max-w-[160px] text-sm text-gray-600">
                          <span className="truncate block font-mono" title={transactionId !== EMPTY ? transactionId : undefined}>
                            {transactionId}
                          </span>
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap max-w-[200px] text-sm text-gray-600">
                          <span className="truncate block" title={services !== EMPTY ? services : undefined}>
                            {services}
                          </span>
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-600">
                          <span className="font-medium">{bf.actionCount}</span>
                          {bf.failedCount > 0 && (
                            <span className="text-red-500 ml-1">({bf.failedCount} bước lỗi)</span>
                          )}
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap">
                          <StatusBadge status={bf.overallStatus} friendly />
                        </td>
                        <td className="px-4 py-4 text-sm text-gray-600 max-w-[240px]">
                          {lastMessage !== EMPTY ? (
                            <div className="truncate" title={lastMessage}>
                              {lastMessage}
                            </div>
                          ) : (
                            <span className="text-gray-400 italic">Không có dữ liệu</span>
                          )}
                        </td>
                        <td className="px-4 py-4 whitespace-nowrap text-sm">
                          <Link
                            to={`/business-flows/${encodeURIComponent(bf.orderCode)}`}
                            className="text-blue-600 hover:text-blue-800 font-medium"
                          >
                            Xem chi tiết nghiệp vụ
                          </Link>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-between">
                <button
                  type="button"
                  onClick={handlePrevious}
                  disabled={page <= 1}
                  className="px-4 py-2 bg-white border border-gray-300 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  Trước
                </button>
                <span className="text-sm text-gray-600">
                  Trang {page} / {totalPages}
                </span>
                <button
                  type="button"
                  onClick={handleNext}
                  disabled={page >= totalPages}
                  className="px-4 py-2 bg-white border border-gray-300 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  Sau
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
