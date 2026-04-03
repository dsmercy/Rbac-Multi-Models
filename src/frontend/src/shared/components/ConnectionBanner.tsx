import { useSignalRStore } from '@/shared/stores/signalRStore';

/**
 * Persistent top banner shown when the SignalR connection is lost after
 * exhausting all reconnect retries. Disappears automatically when the
 * connection recovers (state returns to 'connected').
 *
 * Rendered inside TenantLayout — always visible above page content.
 */
export function ConnectionBanner() {
  const state = useSignalRStore((s) => s.state);

  if (state === 'connected' || state === 'connecting') return null;

  const isReconnecting = state === 'reconnecting';

  return (
    <div
      role="status"
      aria-live="polite"
      className={`flex items-center gap-2 px-4 py-2 text-sm font-medium ${
        isReconnecting
          ? 'bg-amber-50 text-amber-800 border-b border-amber-200'
          : 'bg-red-50 text-red-800 border-b border-red-200'
      }`}
    >
      {/* Spinner for reconnecting / static icon for disconnected */}
      {isReconnecting ? (
        <svg
          className="h-4 w-4 animate-spin"
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <circle
            className="opacity-25"
            cx="12" cy="12" r="10"
            stroke="currentColor" strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8v4l3-3-3-3v4a8 8 0 100 16v-4l-3 3 3 3v-4a8 8 0 01-8-8z"
          />
        </svg>
      ) : (
        <svg
          className="h-4 w-4"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden="true"
        >
          <path
            fillRule="evenodd"
            d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z"
            clipRule="evenodd"
          />
        </svg>
      )}

      <span>
        {isReconnecting
          ? 'Real-time connection interrupted — attempting to reconnect…'
          : 'Real-time updates unavailable. Refresh the page to reconnect.'}
      </span>
    </div>
  );
}
