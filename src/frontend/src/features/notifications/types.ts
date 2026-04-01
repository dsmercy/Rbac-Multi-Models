export type NotificationType = 'RoleChanged' | 'PolicyChanged' | 'DelegationRequest' | 'PolicyAlert';
export interface RbacNotification { id: string; type: NotificationType; title: string; description: string; resourceId: string | null; isRead: boolean; createdAt: string; }
