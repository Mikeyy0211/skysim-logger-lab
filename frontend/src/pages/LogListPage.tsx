import { useEffect, useState } from 'react';
import type { KeyboardEvent, ChangeEvent } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { getLogFlows } from '../services/logFlowService';
import type { LogFlowSummary } from '../types/logFlow';

const PAGE_SIZE = 10;

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const d = new Date(value);
  if (isNaN(d.getTime())) return EMPTY;
  return d.toLocaleString();
}

function resolveDisplayUserEmail(flow: LogFlowSummary): string {
  return (
    flow.userEmail?.trim() ||
    flow.customerEmail?.trim() ||
    flow.userId?.trim() ||
    EMPTY
  );
}

function resolveDisplayOrder(flow: LogFlowSummary): string {
  return (
    flow.orderCode?.trim() ||
    flow.orderId?.trim() ||
    flow.paymentId?.trim() ||
    EMPTY
  );
}

function resolveDisplayService(flow: LogFlowSummary): string {
  return flow.lastServiceName?.trim() || EMPTY;
}

function resolveDisplayAction(flow: LogFlowSummary): string {
  return flow.lastActionType?.trim() || EMPTY;
}

function resolveDisplayMessage(flow: LogFlowSummary): string {
  return flow.lastMessage?.trim() || EMPTY;
}

export function LogListPage() {
  const [flows, setFlows] = useState<LogFlowSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [flowType, setFlowType] = useState('');
  const [checkoutType, setCheckoutType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  // Pagination state
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
      if (search.trim()) params.search = search.trim();
      if (status) params.status = status;
      if (flowType) params.flowType = flowType;
      if (checkoutType) params.checkoutType = checkoutType;
      if (fromDate) params.fromDate = fromDate;
      if (toDate) params.toDate = toDate;

      const paged = await getLogFlows(params as Parameters<typeof getLogFlows>[0]);

      setFlows(paged.items);
      setPage(paged.page);
      setTotalPages(paged.totalPages);
    } catch {
      setError('Unable to load log flows.');
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

  function handleStatusChange(e: ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setStatus(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleFlowTypeChange(e: ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setFlowType(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleCheckoutTypeChange(e: ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setCheckoutType(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleFromDateChange(e: ChangeEvent<HTMLInputElement>) {
    const val = e.target.value;
    setFromDate(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleToDateChange(e: ChangeEvent<HTMLInputElement>) {
    const val = e.target.value;
    setToDate(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleReset() {
    setSearch('');
    setStatus('');
    setFlowType('');
    setCheckoutType('');
    setFromDate('');
    setToDate('');
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
        title="Flow Monitoring"
        subtitle="Search and inspect backend processing flows"
      />

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm mb-6">
        <div className="p-4 border-b border-gray-200">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4">
            <div className="lg:col-span-2">
              <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
                Search
              </label>
              <input
                id="search"
                type="text"
                placeholder="Search email, order code, payment id, transaction id..."
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={handleSearchKeyDown}
              />
            </div>
            <div>
              <label htmlFor="status" className="block text-sm font-medium text-gray-700 mb-1">
                Status
              </label>
              <select
                id="status"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={status}
                onChange={handleStatusChange}
              >
                <option value="">All Statuses</option>
                <option value="SUCCESS">Success</option>
                <option value="FAILED">Failed</option>
                <option value="RUNNING">Running</option>
                <option value="PARTIAL_FAILED">Partial Failed</option>
              </select>
            </div>
            <div>
              <label htmlFor="flowType" className="block text-sm font-medium text-gray-700 mb-1">
                Flow Type
              </label>
              <select
                id="flowType"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={flowType}
                onChange={handleFlowTypeChange}
              >
                <option value="">All Flow Types</option>
                <option value="CHECKOUT_ESIM">Checkout eSIM</option>
                <option value="HTTP_ACTION">HTTP Action</option>
              </select>
            </div>
            <div>
              <label htmlFor="checkoutType" className="block text-sm font-medium text-gray-700 mb-1">
                Checkout Type
              </label>
              <select
                id="checkoutType"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={checkoutType}
                onChange={handleCheckoutTypeChange}
              >
                <option value="">All Types</option>
                <option value="GUEST">Guest</option>
                <option value="AUTHENTICATED">Authenticated</option>
              </select>
            </div>
            <div>
              <label htmlFor="fromDate" className="block text-sm font-medium text-gray-700 mb-1">
                From Date
              </label>
              <input
                id="fromDate"
                type="date"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={fromDate}
                onChange={handleFromDateChange}
              />
            </div>
            <div>
              <label htmlFor="toDate" className="block text-sm font-medium text-gray-700 mb-1">
                To Date
              </label>
              <input
                id="toDate"
                type="date"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-sm"
                value={toDate}
                onChange={handleToDateChange}
              />
            </div>
          </div>

          <div className="mt-4 flex items-center gap-2">
            <button
              type="button"
              onClick={handleSearchClick}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
            >
              Search
            </button>
            <button
              type="button"
              onClick={handleReset}
              className="px-4 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-gray-400 transition-colors"
            >
              Reset Filters
            </button>
          </div>
        </div>

        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center p-12">
              <svg className="animate-spin w-8 h-8 text-blue-600 mb-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <p className="text-sm text-gray-500">Loading log flows...</p>
            </div>
          ) : error ? (
            <div className="p-6">
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
                <p className="font-medium mb-1">Failed to load log flows</p>
                <p className="text-sm">{error}</p>
                <button
                  onClick={() => fetchFlows(page)}
                  className="mt-3 px-4 py-2 bg-red-100 text-red-700 text-sm font-medium rounded-lg hover:bg-red-200 focus:outline-none focus:ring-2 focus:ring-red-500 transition-colors"
                >
                  Retry
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
              <p className="text-base font-medium text-gray-700 mb-1">No log flows found</p>
              <p className="text-sm text-gray-500 text-center max-w-sm">
                No flows match your filters. Try adjusting your search criteria or clear the filters.
              </p>
              <button
                onClick={handleReset}
                className="mt-4 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
              >
                Clear Filters
              </button>
            </div>
          ) : (
            <>
              <table className="w-full">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Time
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      User Email
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Order Code
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Service
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Action
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Status
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Message
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {flows.map((flow) => {
                    const userEmail = resolveDisplayUserEmail(flow);
                    const order = resolveDisplayOrder(flow);
                    const service = resolveDisplayService(flow);
                    const action = resolveDisplayAction(flow);
                    const message = resolveDisplayMessage(flow);

                    return (
                      <tr key={flow.flowId} className="hover:bg-gray-50">
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {formatDate(flow.updatedAt)}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap max-w-[220px] text-sm text-gray-700">
                          <span className="truncate block" title={userEmail !== EMPTY ? userEmail : undefined}>
                            {userEmail}
                          </span>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap max-w-[200px] text-sm text-gray-600 font-mono">
                          <span className="truncate block" title={order !== EMPTY ? order : undefined}>
                            {order}
                          </span>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                          {service !== EMPTY ? (
                            service
                          ) : (
                            <span className="text-gray-400 italic">unknown</span>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                          {action}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <StatusBadge status={flow.status} />
                        </td>
                        <td className="px-6 py-4 text-sm text-gray-600 max-w-[260px]">
                          {message !== EMPTY ? (
                            <div className="truncate" title={message}>
                              {message}
                            </div>
                          ) : (
                            <span className="text-gray-400 italic">unknown</span>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm">
                          <Link
                            to={`/logs/${flow.flowId}`}
                            className="text-blue-600 hover:text-blue-800 font-medium"
                          >
                            View Detail
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
                  Previous
                </button>
                <span className="text-sm text-gray-600">
                  Page {page} of {totalPages}
                </span>
                <button
                  type="button"
                  onClick={handleNext}
                  disabled={page >= totalPages}
                  className="px-4 py-2 bg-white border border-gray-300 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  Next
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
