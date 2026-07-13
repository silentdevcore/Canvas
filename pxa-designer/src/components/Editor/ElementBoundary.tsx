import React from 'react';

interface Props {
  /** Element name, shown in the fallback chip and logged for diagnosis. */
  name?: string;
  children: React.ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Catches a render error from a single canvas element so one problematic element (e.g. a value that
 * trips a browser-specific API) can't blank the entire designer. The failing element shows a small
 * "⚠ name" chip in place, and the real error + component stack is logged to the console so the culprit
 * can be identified. Imported migrated reports are the main beneficiary — they can contain values the
 * editor doesn't normally produce.
 */
export class ElementBoundary extends React.Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    // eslint-disable-next-line no-console
    console.error(
      `[Designer] element "${this.props.name ?? '?'}" failed to render:`,
      error,
      info.componentStack,
    );
  }

  render(): React.ReactNode {
    if (this.state.error) {
      return (
        <div
          title={`${this.props.name ?? 'element'}: ${this.state.error.message}`}
          style={{
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
            background: '#fef2f2',
            border: '1px dashed #f87171',
            color: '#b91c1c',
            fontSize: 10,
            lineHeight: 1.2,
            textAlign: 'center',
            overflow: 'hidden',
            padding: 2,
            boxSizing: 'border-box',
          }}
        >
          ⚠ {this.props.name ?? 'element'}
        </div>
      );
    }
    return this.props.children;
  }
}
