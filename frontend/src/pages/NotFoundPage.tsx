import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="flex flex-col items-center justify-center min-h-[60vh] p-6">
      <div className="text-center">
        <h1 className="text-6xl font-bold text-gray-200 mb-4">404</h1>
        <h2 className="text-xl font-semibold text-gray-800 mb-2">Không tìm thấy trang</h2>
        <p className="text-sm text-gray-500 mb-6">
          Trang bạn đang tìm không tồn tại.
        </p>
        <Link
          to="/logs"
          className="inline-flex items-center px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors"
        >
          Quay lại Nhật ký
        </Link>
      </div>
    </div>
  );
}
