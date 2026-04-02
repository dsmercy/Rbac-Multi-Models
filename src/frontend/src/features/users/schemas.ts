import { z } from 'zod';

export const createUserSchema = z.object({
  email: z.string().email('Invalid email address').max(320),
  displayName: z.string().min(2, 'Display name must be at least 2 characters').max(255),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Must contain an uppercase letter')
    .regex(/[0-9]/, 'Must contain a number')
    .regex(/[^A-Za-z0-9]/, 'Must contain a special character'),
});

export const assignRoleSchema = z
  .object({
    roleId: z.string().uuid('Must select a valid role'),
    scopeId: z.string().uuid('Must select a valid scope'),
    expiresAt: z.string().datetime({ offset: true }).optional(),
  })
  .refine(
    (data) => {
      if (!data.expiresAt) return true;
      return new Date(data.expiresAt) > new Date();
    },
    { message: 'Expiry date must be in the future', path: ['expiresAt'] }
  );

export type CreateUserSchema = z.infer<typeof createUserSchema>;
export type AssignRoleSchema = z.infer<typeof assignRoleSchema>;
