import { z } from 'zod';

export const createDelegationSchema = z
  .object({
    delegateeUserId: z.string().uuid('Must select a valid user'),
    permissionIds: z
      .array(z.string().uuid())
      .min(1, 'Select at least one permission to delegate'),
    scopeId: z.string().uuid('Must select a valid scope'),
    expiresAt: z.string().datetime({ offset: true, message: 'Expiry date is required' }),
  })
  .refine(
    (data) => new Date(data.expiresAt) > new Date(),
    { message: 'Expiry date must be in the future', path: ['expiresAt'] }
  );

export type CreateDelegationSchema = z.infer<typeof createDelegationSchema>;
