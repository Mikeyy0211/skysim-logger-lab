export type CheckoutType = 'GUEST' | 'AUTHENTICATED';

interface CheckoutTypeBadgeProps {
  checkoutType?: string | null;
}

const checkoutTypeStyles: Record<CheckoutType, string> = {
  GUEST: 'bg-purple-100 text-purple-800',
  AUTHENTICATED: 'bg-blue-100 text-blue-800',
};

export function CheckoutTypeBadge({ checkoutType }: CheckoutTypeBadgeProps) {
  if (!checkoutType) {
    return (
      <span className="px-2 py-1 text-xs font-medium rounded-full bg-gray-100 text-gray-600">
        —
      </span>
    );
  }

  const normalized = checkoutType.trim().toUpperCase() as CheckoutType;
  const style = checkoutTypeStyles[normalized] ?? 'bg-gray-100 text-gray-800';
  const label = normalized.replace(/_/g, ' ');

  return (
    <span className={`px-2 py-1 text-xs font-medium rounded-full ${style}`}>
      {label}
    </span>
  );
}
