import { Link, useLocation } from 'react-router-dom';

const navItems = [
  { path: '/dashboard', label: 'Tổng quan' },
  { path: '/logs', label: 'Nhật ký' },
];

export function Sidebar({
  isCollapsed,
  onToggle,
}: {
  isCollapsed: boolean;
  onToggle: () => void;
}) {
  const location = useLocation();

  const isActive = (path: string) =>
    location.pathname === path || (path === '/dashboard' && location.pathname === '/');

  return (
    <aside
      className="relative flex flex-col bg-gray-900 text-white transition-all duration-300 ease-in-out"
      style={{ width: isCollapsed ? '64px' : '256px' }}
    >
      {/* Logo / Title */}
      <div className="p-4 border-b border-gray-700 flex items-center overflow-hidden">
        <div className="w-8 h-8 bg-blue-600 rounded flex items-center justify-center flex-shrink-0">
          <svg
            className="w-5 h-5 text-white"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
        </div>
        {!isCollapsed && (
          <span className="ml-3 text-lg font-bold whitespace-nowrap overflow-hidden">
            SkySim Logger
          </span>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 p-3">
        <ul className="space-y-1">
          {navItems.map((item) => (
            <li key={item.path}>
              <Link
                to={item.path}
                className={`flex items-center rounded-lg transition-colors overflow-hidden ${
                  isCollapsed ? 'justify-center px-3 py-3' : 'px-4 py-2'
                } ${
                  isActive(item.path)
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                }`}
              >
                {item.path === '/dashboard' ? (
                  <svg
                    className="w-5 h-5 flex-shrink-0"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z"
                    />
                  </svg>
                ) : (
                  <svg
                    className="w-5 h-5 flex-shrink-0"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
                    />
                  </svg>
                )}
                {!isCollapsed && (
                  <span className="ml-3 whitespace-nowrap overflow-hidden">{item.label}</span>
                )}
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      {/* Collapse toggle button */}
      <button
        onClick={onToggle}
        className="absolute -right-3 top-1/2 -translate-y-1/2 w-6 h-6 bg-gray-700 hover:bg-gray-600 border border-gray-600 rounded-full flex items-center justify-center text-gray-300 hover:text-white transition-colors shadow-sm z-10 focus:outline-none focus:ring-2 focus:ring-blue-500"
        title={isCollapsed ? 'Mở rộng thanh điều hướng' : 'Thu gọn thanh điều hướng'}
      >
        {isCollapsed ? (
          <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2.5}
              d="M9 5l7 7-7 7"
            />
          </svg>
        ) : (
          <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2.5}
              d="M15 19l-7-7 7-7"
            />
          </svg>
        )}
      </button>
    </aside>
  );
}
