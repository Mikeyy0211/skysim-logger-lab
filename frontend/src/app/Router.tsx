import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { AdminLayout } from '../layouts/AdminLayout';
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';
import { LogListPage } from '../pages/LogListPage';
import { LogDetailPage } from '../pages/LogDetailPage';
import { ProtectedRoute } from '../components/ProtectedRoute';

const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <ProtectedRoute><AdminLayout /></ProtectedRoute>,
    children: [
      {
        index: true,
        element: <DashboardPage />,
      },
      {
        path: 'dashboard',
        element: <DashboardPage />,
      },
      {
        path: 'logs',
        element: <LogListPage />,
      },
      {
        path: 'logs/:flowId',
        element: <LogDetailPage />,
      },
    ],
  },
]);

export function Router() {
  return <RouterProvider router={router} />;
}
