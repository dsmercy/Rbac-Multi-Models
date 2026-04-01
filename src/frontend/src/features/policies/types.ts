export type PolicyEffect = 'Allow' | 'Deny';
export type ConditionOperator = 'And' | 'Or' | 'Not';
export interface ConditionNode { operator: ConditionOperator; conditions: ConditionLeaf[]; }
export interface ConditionLeaf { attribute: string; operator: string; value: string | number | boolean; }
export interface Policy { id: string; tenantId: string; name: string; description: string | null; effect: PolicyEffect; conditionTree: ConditionNode; isActive: boolean; createdAt: string; updatedAt: string | null; }
export interface CreatePolicyInput { name: string; description?: string; effect: PolicyEffect; conditionTree: ConditionNode; }
export interface UpdatePolicyInput { name?: string; description?: string; effect?: PolicyEffect; conditionTree?: ConditionNode; isActive?: boolean; }
