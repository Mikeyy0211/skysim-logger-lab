import { useNavigate } from 'react-router-dom';
import { useAppDispatch } from '../app/hooks';
import { clearCredentials } from '../features/auth/authSlice';
import { logout } from '../services/authService';

export function Header() {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  const handleLogout = () => {
    logout();
    dispatch(clearCredentials());
    navigate('/login');
  };

  return (
    <header className="bg-white border-b border-gray-200 px-6 py-4 flex justify-between items-center">
      <h2 className="text-lg font-semibold text-gray-800">Quản trị nhật ký SkySim</h2>
      <button
        onClick={handleLogout}
        className="px-4 py-2 text-sm text-gray-600 hover:text-gray-900 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
      >
        Đăng xuất
      </button>
    </header>
  );
}
