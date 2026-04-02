import { z } from 'zod';

// Recursive condition tree — Zod handles this with z.lazy()
const conditionLeafSchema = z.object({
  attribute: z.string().min(1, 'Attribute is required'),
  operator: z.string().min(1, 'Operator is required'),
  value: z.union([z.string(), z.number(), z.boolean()]),
});

const conditionNodeSchema: z.ZodType<{
  operator: 'And' | 'Or' | 'Not';
  conditions: Array<{ attribute: string; operator: string; value: string | number | boolean }>;
}> = z.object({
  operator: z.enum(['And', 'Or', 'Not']),
  conditions: z.array(conditionLeafSchema).min(1, 'At least one condition is required'),
});

export const createPolicySchema = z.object({
  name: z.string().min(2, 'Name must be at least 2 characters').max(255),
  description: z.string().max(1000).optional(),
  effect: z.enum(['Allow', 'Deny']),
  conditionTree: conditionNodeSchema,
});

export const updatePolicySchema = createPolicySchema.partial().extend({
  isActive: z.boolean().optional(),
});

export type CreatePolicySchema = z.infer<typeof createPolicySchema>;
export type UpdatePolicySchema = z.infer<typeof updatePolicySchema>;
