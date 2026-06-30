import type { FlowStatus } from '../data/mockData';

interface StatusBadgeProps {
  status: FlowStatus;
}

const statusStyles: Record<FlowStatus, string> = {
  SUCCESS: 'bg-green-100 text-green-800',
  FAILED: 'bg-red-100 text-red-800',
  RUNNING: 'bg-blue-100 text-blue-800',
  PARTIAL_FAILED: 'bg-amber-100 text-amber-800',
};

export function StatusBadge({ status }: StatusBadgeProps) {
  return (
    <span className={`px-2 py-1 text-xs font-medium rounded-full ${statusStyles[status]}`}>
      {status.replace('_', ' ')}
    </span>
  );
}
