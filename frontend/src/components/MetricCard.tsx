interface MetricCardProps {
  title: string;
  value: string | number | null | undefined;
}

export function MetricCard({ title, value }: MetricCardProps) {
  const display =
    value === null || value === undefined || value === '' ? '—' : String(value);

  return (
    <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-4">
      <p className="text-sm text-gray-600 mb-1">{title}</p>
      <p className="text-2xl font-bold text-gray-900">{display}</p>
    </div>
  );
}
