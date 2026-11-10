import { Component, type ErrorInfo, type ReactNode } from 'react';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  errorMessage: string;
}

export class ErrorBoundary extends Component<
  ErrorBoundaryProps,
  ErrorBoundaryState
> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      errorMessage: '',
    };
  }

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    const message =
      error instanceof Error
        ? error.message
        : 'অপ্রত্যাশিত ত্রুটি ঘটেছে (Unexpected application error).';
    return {
      hasError: true,
      errorMessage: message,
    };
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    console.error('Uncaught UI error:', error, info);
  }

  handleReload = (): void => {
    window.location.reload();
  };

  override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="container py-5">
          <div className="alert alert-danger shadow-sm p-4" role="alert">
            <h4 className="alert-heading mb-2">
              অ্যাপ্লিকেশনে একটি ত্রুটি ঘটেছে (Something went wrong)
            </h4>
            <p className="mb-3">{this.state.errorMessage}</p>
            <button
              type="button"
              className="btn btn-danger"
              onClick={this.handleReload}
            >
              পুনরায় লোড করুন (Reload Page)
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

