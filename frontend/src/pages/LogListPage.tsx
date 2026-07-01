import { useEffect, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Link } from 'react-router-dom';
import { PageHeader } from '../components/PageHeader';
import { StatusBadge } from '../components/StatusBadge';
import { getLogFlows } from '../services/logFlowService';
import type { LogFlowSummary, PagedResponse } from '../types/logFlow';

const PAGE_SIZE = 10;

const EMPTY = '—';

function formatDate(value: string | null | undefined): string {
  if (!value) return EMPTY;
  const d = new Date(value);
  if (isNaN(d.getTime())) return EMPTY;
  return d.toLocaleString();
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

      const response = await getLogFlows(
        params as Parameters<typeof getLogFlows>[0]
      );

      const data = response;
      if (Array.isArray(data)) {
        setFlows(data);
        setTotalPages(1);
        setPage(1);
      } else if (data && typeof data === 'object' && 'items' in data) {
        const paged = data as PagedResponse<LogFlowSummary>;
        setFlows(paged.items);
        setPage(paged.page);
        setTotalPages(paged.totalPages);
      }
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

  function handleStatusChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setStatus(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleFlowTypeChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setFlowType(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleCheckoutTypeChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const val = e.target.value;
    setCheckoutType(val);
    setPage(1);
    fetchFlows(1);
  }

  function handleReset() {
    setSearch('');
    setStatus('');
    setFlowType('');
    setCheckoutType('');
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
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
                Search
              </label>
              <input
                id="search"
                type="text"
                placeholder="Search by email, phone, order ID, payment ID, or flow ID"
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
          </div>

          <div className="mt-4 flex gap-2">
            <button
              type="button"
              onClick={handleSearchClick}
              className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              Search
            </button>
            <button
              type="button"
              onClick={handleReset}
              className="px-4 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-gray-400"
            >
              Reset
            </button>
          </div>
        </div>

        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="flex items-center justify-center p-6">
              <div className="text-gray-500">Loading log flows...</div>
            </div>
          ) : error ? (
            <div className="p-6">
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
                {error}
              </div>
            </div>
          ) : flows.length === 0 ? (
            <div className="p-6">
              <div className="text-center text-gray-500">No log flows found.</div>
            </div>
          ) : (
            <>
              <table className="w-full">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Flow ID
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Status
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Customer
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Checkout
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Last Service
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Last Action
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Updated At
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {flows.map((flow) => (
                    <tr key={flow.flowId} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {flow.flowId}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <StatusBadge status={flow.status} />
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.customerEmail ? (
                          <div>
                            <div>{flow.customerEmail}</div>
                            {flow.customerPhone && (
                              <div className="text-gray-400">{flow.customerPhone}</div>
                            )}
                          </div>
                        ) : flow.customerPhone ? (
                          flow.customerPhone
                        ) : (
                          EMPTY
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.checkoutType || EMPTY}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.lastServiceName || EMPTY}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {flow.lastActionType || EMPTY}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {formatDate(flow.updatedAt)}
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
                  ))}
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
