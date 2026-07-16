import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAppSelector } from '../app/hooks';
import { setCredentials } from '../features/auth/authSlice';
import { login as authenticate } from '../services/authService';
import { useAppDispatch } from '../app/hooks';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const dispatch = useAppDispatch();
  const isAuthenticated = useAppSelector((s) => s.auth.isAuthenticated);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const from = (location.state as { from?: { pathname?: string } })?.from?.pathname || '/dashboard';

  useEffect(() => {
    if (isAuthenticated) {
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, navigate, from]);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError('');

    if (!username.trim() || !password.trim()) {
      setError('Vui lòng nhập tên đăng nhập và mật khẩu.');
      return;
    }

    setIsLoading(true);

    const result = await authenticate(username, password);

    setIsLoading(false);

    if (result.success && result.accessToken) {
      dispatch(setCredentials({ accessToken: result.accessToken, username }));
    } else {
      setError(result.error || 'Không thể đăng nhập. Vui lòng thử lại.');
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50">
      <div className="w-full max-w-md p-8 bg-white rounded-lg border border-gray-200 shadow-sm">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-gray-900">Quản trị nhật ký SkySim</h1>
          <p className="text-gray-600 mt-2">Đăng nhập để theo dõi nhật ký hệ thống</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
              {error}
            </div>
          )}

          <div>
            <label htmlFor="username" className="block text-sm font-medium text-gray-700 mb-1">
              Tên đăng nhập
            </label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              placeholder="Nhập tên đăng nhập"
              disabled={isLoading}
              autoComplete="username"
            />
          </div>

          <div>
            <label htmlFor="password" className="block text-sm font-medium text-gray-700 mb-1">
              Mật khẩu
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              placeholder="Nhập mật khẩu"
              disabled={isLoading}
              autoComplete="current-password"
            />
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-2 px-4 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? 'Đang đăng nhập...' : 'Đăng nhập'}
          </button>
        </form>

        <p className="text-center text-sm text-gray-500 mt-6">
          Sử dụng tài khoản nội bộ để truy cập màn hình theo dõi nhật ký
        </p>
      </div>
    </div>
  );
}
