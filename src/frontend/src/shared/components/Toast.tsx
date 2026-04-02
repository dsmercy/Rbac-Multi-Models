import { useToastStore } from '@/shared/stores/toastStore';
import { cn } from '@/shared/utils/cn';

const variantStyles = {
  success: 'bg-green-50 border-green-200 text-green-800',
  error:   'bg-red-50 border-red-200 text-red-800',
  warning: 'bg-amber-50 border-amber-200 text-amber-800',
  info:    'bg-blue-50 border-blue-200 text-blue-800',
};

const variantIcon = {
  success: '✓',
  error:   '✕',
  warning: '⚠',
  info:    'ℹ',
};

/**
 * Global toast container — mount once in main.tsx above RouterProvider.
 * Reads from useToastStore; no props needed.
 */
export function ToastContainer() {
  const { toasts, remove } = useToastStore();

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 w-80" role="region" aria-label="Notifications">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role="alert"
          className={cn(
            'flex items-start gap-3 px-4 py-3 rounded-lg border shadow-md text-sm animate-in slide-in-from-bottom-2',
            variantStyles[toast.variant]
          )}
        >
          <span className="font-bold text-base leading-none mt-0.5" aria-hidden>
            {variantIcon[toast.variant]}
          </span>
          <div className="flex-1 min-w-0">
            <p className="font-medium">{toast.title}</p>
            {toast.description && (
              <p className="mt-0.5 opacity-80 text-xs">{toast.description}</p>
            )}
          </div>
          <button
            onClick={() => remove(toast.id)}
            aria-label="Dismiss notification"
            className="flex-shrink-0 opacity-60 hover:opacity-100 transition-opacity"
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  );
}
