import { z } from 'zod';

export const createDelegationSchema = z
  .object({
    delegateeUserId: z.string().uuid('Must select a valid user'),
    permissionIds: z
      .array(z.string().uuid())
      .min(1, 'Select at least one permission to delegate'),
    scopeId: z.string().uuid('Must select a valid scope'),
    // datetime-local inputs produce "YYYY-MM-DDTHH:mm" — validated as a string here;
    // converted to full ISO 8601 string in the submit handler before API call
    expiresAt: z.string().min(1, 'Expiry date is required'),
  })
  .refine(
    (data) => {
      const d = new Date(data.expiresAt);
      return !isNaN(d.getTime()) && d > new Date();
    },
    { message: 'Expiry date must be in the future', path: ['expiresAt'] }
  );

export type CreateDelegationSchema = z.infer<typeof createDelegationSchema>;
