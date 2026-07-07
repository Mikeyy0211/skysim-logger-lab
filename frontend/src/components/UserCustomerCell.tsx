const EMPTY = '—';

function truncate(value: string, maxLen: number): { display: string; full: string } {
  const display = value.length > maxLen ? value.substring(0, maxLen) + '…' : value;
  return { display, full: value };
}

/**
 * Shared helper component for displaying user/customer info in tables.
 *
 * Display rules (applied in order):
 *  1. userEmail present  →  "User: {userEmail}"  [+ "Customer: {customerEmail}" if different]
 *  2. no userEmail, customerEmail present  →  "Customer: {customerEmail}"
 *  3. no emails, userId present  →  "User ID: {truncated}" (muted, full value in title)
 *  4. nothing  →  "—"
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
  const customerIsDifferent = hasCustomerEmail && customerEmailTrimmed !== userEmailTrimmed;

  if (hasUserEmail) {
    return (
      <div className="flex flex-col gap-0.5 min-w-0">
        <span className="text-sm text-gray-700 truncate" title={userEmailTrimmed}>
          User: {userEmailTrimmed}
        </span>
        {customerIsDifferent && (
          <span className="text-xs text-gray-500 truncate" title={customerEmailTrimmed}>
            Customer: {customerEmailTrimmed}
          </span>
        )}
      </div>
    );
  }

  if (hasCustomerEmail) {
    return (
      <div className="flex flex-col gap-0.5 min-w-0">
        <span className="text-sm text-gray-700 truncate" title={customerEmailTrimmed}>
          Customer: {customerEmailTrimmed}
        </span>
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
          User ID: {display}
        </span>
      </div>
    );
  }

  return (
    <span className="text-sm text-gray-400">{EMPTY}</span>
  );
}
