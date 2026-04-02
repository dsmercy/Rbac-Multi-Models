interface EmptyStateProps {
  icon?: string;
  title: string;
  description?: string;
  action?: React.ReactNode;
}

/**
 * Consistent empty-state across all views.
 *
 * @example
 * <EmptyState
 *   icon="🗂"
 *   title="No roles yet"
 *   description="Create your first role to start assigning permissions."
 *   action={<Button onClick={openCreate}>Create role</Button>}
 * />
 */
export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center space-y-3">
      {icon && <span className="text-4xl" aria-hidden>{icon}</span>}
      <h3 className="text-base font-semibold">{title}</h3>
      {description && (
        <p className="text-sm text-muted-foreground max-w-xs">{description}</p>
      )}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
