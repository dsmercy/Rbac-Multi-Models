import * as Dialog from '@radix-ui/react-dialog';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Reusable confirmation modal.
 * Use `destructive` for irreversible actions (delete, revoke) — renders the
 * confirm button in red to signal risk.
 *
 * @example
 * <ConfirmDialog
 *   open={!!deletingRoleId}
 *   title="Delete role"
 *   description="This will deactivate all assignments for this role. This cannot be undone."
 *   confirmLabel="Delete"
 *   destructive
 *   isLoading={isDeleting}
 *   onConfirm={handleDelete}
 *   onCancel={() => setDeletingRoleId(null)}
 * />
 */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  isLoading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={(v) => !v && onCancel()}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 bg-black/40 z-40" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 -translate-x-1/2 -translate-y-1/2 w-full max-w-sm bg-card border rounded-xl shadow-xl p-6 space-y-4 focus:outline-none"
          aria-describedby="confirm-desc"
        >
          <Dialog.Title className="text-base font-semibold">{title}</Dialog.Title>
          <Dialog.Description id="confirm-desc" className="text-sm text-muted-foreground">
            {description}
          </Dialog.Description>

          <div className="flex justify-end gap-2 pt-2">
            <button
              onClick={onCancel}
              disabled={isLoading}
              className="px-4 py-2 text-sm rounded-md border hover:bg-accent transition-colors disabled:opacity-50"
            >
              {cancelLabel}
            </button>
            <button
              onClick={onConfirm}
              disabled={isLoading}
              className={`px-4 py-2 text-sm rounded-md font-medium transition-colors disabled:opacity-50 ${
                destructive
                  ? 'bg-red-600 text-white hover:bg-red-700'
                  : 'bg-primary text-primary-foreground hover:bg-primary/90'
              }`}
            >
              {isLoading ? 'Please wait…' : confirmLabel}
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
