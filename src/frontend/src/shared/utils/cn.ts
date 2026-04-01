import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Merges Tailwind classes safely — handles conditional classes and deduplication. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
