import type { FlowStatus } from '../types/logFlow';
import { getBusinessStatusLabel, getTechnicalStatusLabel } from '../features/logs/businessStatusDisplay';

interface StatusBadgeProps {
  status?: string | null;
  friendly?: boolean;
}

const statusStyles: Record<FlowStatus, string> = {
  SUCCESS: 'bg-green-100 text-green-800',
  FAILED: 'bg-red-100 text-red-800',
  RUNNING: 'bg-blue-100 text-blue-800',
  IN_PROGRESS: 'bg-blue-100 text-blue-800',
  PROCESSING: 'bg-blue-100 text-blue-800',
  PARTIAL_FAILED: 'bg-amber-100 text-amber-800',
};

export function StatusBadge({ status, friendly = false }: StatusBadgeProps) {
  const normalizedStatus = status?.trim() || 'UNKNOWN';
  const style = statusStyles[normalizedStatus as FlowStatus] ?? 'bg-gray-100 text-gray-800';
  const label = friendly ? getBusinessStatusLabel(status) : getTechnicalStatusLabel(status);

  return (
    <span title={friendly ? getTechnicalStatusLabel(status) : undefined} className={`px-2 py-1 text-xs font-medium rounded-full ${style}`}>
      {label}
    </span>
  );
}
