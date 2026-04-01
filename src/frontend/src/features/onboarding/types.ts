export type OnboardingStep = 'create-role' | 'assign-permissions' | 'add-user';
export interface OnboardingState { currentStep: OnboardingStep; completedSteps: OnboardingStep[]; isDismissed: boolean; }
