export default function NeedsReviewPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Needs Review</h1>
        <p className="mt-1 text-sm text-gray-500">
          Transactions requiring human approval or classification.
        </p>
      </div>
      <div className="bg-white shadow rounded-lg border border-gray-200 p-6">
        <p className="text-gray-500 text-center py-8">No items need review.</p>
      </div>
    </div>
  );
}
