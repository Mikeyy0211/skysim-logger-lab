import type { FlowStatus } from '../types/logFlow';

interface StatusBadgeProps {
  status?: string | null;
}

const statusStyles: Record<FlowStatus, string> = {
  SUCCESS: 'bg-green-100 text-green-800',
  FAILED: 'bg-red-100 text-red-800',
  RUNNING: 'bg-blue-100 text-blue-800',
  PARTIAL_FAILED: 'bg-amber-100 text-amber-800',
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const normalizedStatus = status?.trim() || 'UNKNOWN';
  const style = statusStyles[normalizedStatus as FlowStatus] ?? 'bg-gray-100 text-gray-800';
  const label = normalizedStatus.replace(/_/g, ' ');

  return (
    <span className={`px-2 py-1 text-xs font-medium rounded-full ${style}`}>
      {label}
    </span>
  );
}
