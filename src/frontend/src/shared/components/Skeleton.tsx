import { cn } from '@/shared/utils/cn';

interface SkeletonProps {
  className?: string;
}

/**
 * Generic skeleton block for individual loading states.
 */
export function SkeletonBlock({ className }: SkeletonProps) {
  return (
    <div
      className={cn('animate-pulse rounded-md bg-muted', className)}
    />
  );
}

/**
 * Full-page loading state — used by Suspense boundaries and AuthGuard.
 */
export function Skeleton() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="space-y-3 w-48">
        <SkeletonBlock className="h-4 w-full" />
        <SkeletonBlock className="h-4 w-3/4" />
        <SkeletonBlock className="h-4 w-1/2" />
      </div>
    </div>
  );
}
