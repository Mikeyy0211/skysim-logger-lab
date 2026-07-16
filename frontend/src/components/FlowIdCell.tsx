import { useState } from 'react';

interface FlowIdCellProps {
  flowId: string;
}

function truncateFlowId(flowId: string): string {
  if (flowId.length <= 16) return flowId;
  return `${flowId.slice(0, 8)}...${flowId.slice(-4)}`;
}

export function FlowIdCell({ flowId }: FlowIdCellProps) {
  const [copied, setCopied] = useState(false);

  const displayId = truncateFlowId(flowId);

  async function handleCopy(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(flowId);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // fallback: select text
      const el = e.currentTarget.parentElement;
      if (el) {
        const range = document.createRange();
        range.selectNodeContents(el);
        window.getSelection()?.removeAllRanges();
        window.getSelection()?.addRange(range);
      }
    }
  }

  return (
    <div className="flex items-center gap-1">
      <span
        className="text-sm text-gray-900 font-mono cursor-default"
        title={flowId}
      >
        {displayId}
      </span>
      <button
        onClick={handleCopy}
        title="Sao chép FlowId"
        className="p-0.5 text-gray-400 hover:text-gray-600 focus:outline-none"
      >
        {copied ? (
          <svg className="w-3.5 h-3.5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
        ) : (
          <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
        )}
      </button>
    </div>
  );
}
