import { z } from 'zod';

export const createPermissionSchema = z.object({
  action: z
    .string()
    .min(3, 'Action must be at least 3 characters')
    .max(255)
    .regex(/^[a-z0-9]+:[a-z0-9_-]+$/, 'Action must be in format "resource:verb" e.g. role:create'),
  resourceType: z
    .string()
    .min(2, 'Resource type is required')
    .max(255),
  description: z.string().max(1000).optional(),
});

export type CreatePermissionSchema = z.infer<typeof createPermissionSchema>;
