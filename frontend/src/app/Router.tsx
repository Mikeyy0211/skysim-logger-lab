import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { AdminLayout } from '../layouts/AdminLayout';
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';
import { LogListPage } from '../pages/LogListPage';
import { LogDetailPage } from '../pages/LogDetailPage';
import { BusinessFlowDetailPage } from '../pages/BusinessFlowDetailPage';
import { NotFoundPage } from '../pages/NotFoundPage';
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
      {
        path: 'business-flows/:orderCode',
        element: <BusinessFlowDetailPage />,
      },
      {
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);

export function Router() {
  return <RouterProvider router={router} />;
}
