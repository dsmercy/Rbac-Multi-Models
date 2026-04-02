import React from 'react';
import { cn } from '@/shared/utils/cn';

interface SkeletonProps {
  className?: string;
  style?: React.CSSProperties;
}

/** Single skeleton block for inline loading placeholders. */
export function SkeletonBlock({ className, style }: SkeletonProps) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)} style={style} />;
}

interface SkeletonTableProps {
  rows?: number;
  cols?: number;
}

/** Skeleton that matches a table's shape — used inside data table views. */
export function SkeletonTable({ rows = 5, cols = 4 }: SkeletonTableProps) {
  return (
    <div className="space-y-2 p-4">
      {/* Header row */}
      <div className="flex gap-3 pb-2 border-b">
        {Array.from({ length: cols }).map((_, i) => (
          <SkeletonBlock key={i} className="h-4 flex-1" />
        ))}
      </div>
      {/* Data rows */}
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex gap-3 py-1">
          {Array.from({ length: cols }).map((_, c) => (
            <SkeletonBlock key={c} className="h-4 flex-1" style={{ opacity: 1 - r * 0.1 }} />
          ))}
        </div>
      ))}
    </div>
  );
}

/** Full-page loading state — used by Suspense boundaries and AuthGuard. */
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
