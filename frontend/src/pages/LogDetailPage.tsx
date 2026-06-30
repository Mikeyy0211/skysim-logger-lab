import { useParams } from 'react-router-dom';

export function LogDetailPage() {
  const { flowId } = useParams<{ flowId: string }>();

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold text-gray-900">Flow Detail</h1>
      <p className="text-gray-600 mt-2">Flow ID: {flowId}</p>
    </div>
  );
}
