import { Component } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/Button';
import { RefreshCw, AlertTriangle } from 'lucide-react';

/**
 * Catches React render errors and displays a recovery UI.
 * Use <ErrorBoundary onRetry={fn}>{children}</ErrorBoundary>
 */
export class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, info) {
    console.error('[ErrorBoundary]', error, info);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
    this.props.onReset?.();
  };

  render() {
    if (this.state.hasError) {
      return <ErrorFallback error={this.state.error} onRetry={this.handleRetry} />;
    }
    return this.props.children;
  }
}

function ErrorFallback({ error, onRetry }) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
      <div className="w-12 h-12 rounded-full bg-red-50 flex items-center justify-center mb-4">
        <AlertTriangle className="w-6 h-6 text-danger" />
      </div>
      <h3 className="text-base font-semibold text-text-primary mb-1">
        {t('common.error', 'An error occurred')}
      </h3>
      <p className="text-sm text-text-muted max-w-sm mb-4">
        {error?.message || t('common.error', 'An unexpected error happened.')}
      </p>
      <div className="flex items-center gap-2 text-xs text-gray-400 mb-4">
        <RefreshCw className="w-3 h-3" />
        <span>
          {new Date().toLocaleTimeString()}
        </span>
      </div>
      <Button onClick={onRetry} variant="outline" size="sm">
        <RefreshCw className="w-4 h-4 mr-1.5 inline" />
        {t('common.retry', 'Retry')}
      </Button>
    </div>
  );
}