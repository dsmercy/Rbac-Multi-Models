import { z } from 'zod';

export const createRoleSchema = z.object({
  name: z
    .string()
    .min(2, 'Name must be at least 2 characters')
    .max(255, 'Name must be under 255 characters')
    .regex(/^[a-zA-Z0-9 _-]+$/, 'Only letters, numbers, spaces, hyphens and underscores'),
  description: z.string().max(1000, 'Description must be under 1000 characters').optional(),
});

export const updateRoleSchema = createRoleSchema.partial();

export type CreateRoleSchema = z.infer<typeof createRoleSchema>;
export type UpdateRoleSchema = z.infer<typeof updateRoleSchema>;
