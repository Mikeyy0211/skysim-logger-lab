const EMPTY = '—';

function truncate(value: string, maxLen: number): { display: string; full: string } {
  const display = value.length > maxLen ? value.substring(0, maxLen) + '…' : value;
  return { display, full: value };
}

/**
 * Shared helper component for displaying user/customer info in tables.
 *
 * User and customer identities are intentionally rendered as independent fields.
 * A guest checkout may have customer data without an authenticated user.
 */
export function UserCustomerCell({
  userEmail,
  customerEmail,
  userId,
  maxUserIdLength = 13,
}: {
  userEmail: string | null | undefined;
  customerEmail: string | null | undefined;
  userId?: string | null | undefined;
  maxUserIdLength?: number;
}) {
  const userEmailTrimmed = userEmail?.trim() || '';
  const customerEmailTrimmed = customerEmail?.trim() || '';
  const userIdTrimmed = userId?.trim() || '';

  const hasUserEmail = userEmailTrimmed.length > 0;
  const hasCustomerEmail = customerEmailTrimmed.length > 0;
  const hasUserId = userIdTrimmed.length > 0;
  if (hasUserEmail || hasCustomerEmail) {
    return (
      <div className="flex flex-col gap-0.5 min-w-0">
        {hasUserEmail && (
          <span className="text-sm text-gray-700 truncate" title={userEmailTrimmed}>
            Người dùng: {userEmailTrimmed}
          </span>
        )}
        {hasCustomerEmail && (
          <span className="text-xs text-gray-500 truncate" title={customerEmailTrimmed}>
            Khách hàng: {customerEmailTrimmed}
          </span>
        )}
      </div>
    );
  }

  if (hasUserId) {
    const { display, full } = truncate(userIdTrimmed, maxUserIdLength);
    return (
      <div className="flex flex-col gap-0.5 min-w-0">
        <span
          className="text-xs text-gray-400 font-mono truncate"
          title={full}
        >
          Mã người dùng: {display}
        </span>
      </div>
    );
  }

  return (
    <span className="text-sm text-gray-400">{EMPTY}</span>
  );
}
