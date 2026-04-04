import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  correlationId: string;
}

/**
 * Route-level error boundary. Catches React render errors and displays a
 * user-friendly fallback instead of a blank screen.
 *
 * Wrap individual routes (not the whole tree) so unaffected pages stay live.
 */
export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, correlationId: '' };
  }

  static getDerivedStateFromError(): State {
    return { hasError: true, correlationId: crypto.randomUUID() };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Report to console in dev — replace with backend error endpoint in Phase 8
    console.error('[ErrorBoundary]', {
      correlationId: this.state.correlationId,
      message: error.message,
      componentStack: info.componentStack,
    });
  }

  handleReset = () => {
    this.setState({ hasError: false, correlationId: '' });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;

      return (
        <div
          role="alert"
          className="flex flex-col items-center justify-center min-h-[60vh] px-6 text-center space-y-4"
        >
          <span className="text-5xl" aria-hidden>⚠️</span>
          <h1 className="text-lg font-semibold">Something went wrong</h1>
          <p className="text-sm text-muted-foreground max-w-sm">
            An unexpected error occurred on this page. Your other data is unaffected.
          </p>
          <p className="text-xs text-muted-foreground font-mono">
            Ref: {this.state.correlationId.slice(0, 8)}
          </p>
          <div className="flex gap-2 mt-2">
            <button
              onClick={this.handleReset}
              className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition-colors"
            >
              Try again
            </button>
            <button
              onClick={() => { this.handleReset(); window.history.back(); }}
              className="px-4 py-2 text-sm border rounded-md hover:bg-accent transition-colors"
            >
              Go back
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
