/**
 * Error handling service following Clean Architecture principles.
 * Provides centralized error handling, recovery strategies, and user feedback.
 */

import {
  DomainError,
  isDomainError,
  isValidationError,
  isNotFoundError,
  isBusinessRuleViolationError,
  isConcurrencyError,
  isAuthorizationError
} from '../../domain/errors/DomainErrors';
import { ILoggerService } from '../../infrastructure/services/LoggerService';

export interface ErrorContext {
  operation: string;
  userId?: string;
  sessionId?: string;
  correlationId?: string;
  metadata?: Record<string, any>;
}

export interface ErrorRecoveryStrategy {
  canHandle: (error: Error) => boolean;
  recover: (error: Error, context: ErrorContext) => Promise<ErrorRecoveryResult>;
}

export interface ErrorRecoveryResult {
  success: boolean;
  recoveredData?: any;
  userMessage?: string;
  shouldRetry?: boolean;
  retryDelay?: number;
}

export interface IErrorHandlerService {
  handle(error: Error, context: ErrorContext): Promise<ErrorRecoveryResult>;
  registerRecoveryStrategy(strategy: ErrorRecoveryStrategy): void;
  getUserFriendlyMessage(error: Error): string;
  shouldRetry(error: Error): boolean;
}

/**
 * Comprehensive error handler with recovery strategies
 */
export class ErrorHandlerService implements IErrorHandlerService {
  private recoveryStrategies: ErrorRecoveryStrategy[] = [];
  private logger: ILoggerService;

  constructor(logger: ILoggerService) {
    this.logger = logger;
    this.initializeDefaultStrategies();
  }

  private initializeDefaultStrategies(): void {
    // Network error recovery
    this.registerRecoveryStrategy({
      canHandle: (error) => error.message.includes('network') || error.message.includes('fetch'),
      recover: async (error, context) => {
        this.logger.warn('Network error detected, attempting recovery', {
          error: error.message,
          operation: context.operation
        });

        // Simple retry logic for network errors
        return {
          success: false, // Let caller decide on retry
          shouldRetry: true,
          retryDelay: 1000,
          userMessage: 'Connection issue. Please try again.'
        };
      }
    });

    // Validation error handling
    this.registerRecoveryStrategy({
      canHandle: (error) => isValidationError(error),
      recover: async (error, context) => {
        const validationError = error as import('../../domain/errors/DomainErrors').ValidationError;
        this.logger.info('Validation error handled', {
          field: validationError.field,
          value: validationError.value,
          operation: context.operation
        });

        return {
          success: false,
          userMessage: `Please check your input${validationError.field ? ` for ${validationError.field}` : ''}.`
        };
      }
    });

    // Not found error handling
    this.registerRecoveryStrategy({
      canHandle: (error) => isNotFoundError(error),
      recover: async (error, context) => {
        const notFoundError = error as import('../../domain/errors/DomainErrors').NotFoundError;
        this.logger.warn('Resource not found', {
          resource: notFoundError.resource,
          id: notFoundError.id,
          operation: context.operation
        });

        return {
          success: false,
          userMessage: `${notFoundError.resource} not found. It may have been deleted.`
        };
      }
    });

    // Business rule violation handling
    this.registerRecoveryStrategy({
      canHandle: (error) => isBusinessRuleViolationError(error),
      recover: async (error, context) => {
        const ruleError = error as import('../../domain/errors/DomainErrors').BusinessRuleViolationError;
        this.logger.warn('Business rule violation', {
          rule: ruleError.rule,
          context: ruleError.context,
          operation: context.operation
        });

        return {
          success: false,
          userMessage: 'This action cannot be completed due to business rules. Please review and try again.'
        };
      }
    });

    // Concurrency error handling
    this.registerRecoveryStrategy({
      canHandle: (error) => isConcurrencyError(error),
      recover: async (error, context) => {
        const concurrencyError = error as import('../../domain/errors/DomainErrors').ConcurrencyError;
        this.logger.warn('Concurrency conflict detected', {
          resource: concurrencyError.resource,
          expectedVersion: concurrencyError.expectedVersion,
          actualVersion: concurrencyError.actualVersion,
          operation: context.operation
        });

        return {
          success: false,
          shouldRetry: false, // Don't auto-retry concurrency errors
          userMessage: 'This item was modified by someone else. Please refresh and try again.'
        };
      }
    });

    // Authorization error handling
    this.registerRecoveryStrategy({
      canHandle: (error) => isAuthorizationError(error),
      recover: async (error, context) => {
        const authError = error as import('../../domain/errors/DomainErrors').AuthorizationError;
        this.logger.error('Authorization failed', {
          action: authError.action,
          resource: authError.resource,
          operation: context.operation
        });

        return {
          success: false,
          userMessage: 'You do not have permission to perform this action.'
        };
      }
    });
  }

  async handle(error: Error, context: ErrorContext): Promise<ErrorRecoveryResult> {
    // Log the error with context
    this.logger.error(`Error in operation: ${context.operation}`, error, {
      userId: context.userId,
      sessionId: context.sessionId,
      correlationId: context.correlationId,
      metadata: context.metadata
    });

    // Try recovery strategies
    for (const strategy of this.recoveryStrategies) {
      if (strategy.canHandle(error)) {
        try {
          const result = await strategy.recover(error, context);
          if (result.success) {
            this.logger.info('Error recovery successful', {
              operation: context.operation,
              strategy: strategy.constructor.name
            });
            return result;
          }
        } catch (recoveryError) {
          this.logger.error('Error recovery failed', recoveryError as Error, {
            operation: context.operation,
            originalError: error.message
          });
        }
      }
    }

    // No recovery strategy worked, return generic error
    return {
      success: false,
      userMessage: this.getUserFriendlyMessage(error)
    };
  }

  registerRecoveryStrategy(strategy: ErrorRecoveryStrategy): void {
    this.recoveryStrategies.unshift(strategy); // Add to front for priority
  }

  getUserFriendlyMessage(error: Error): string {
    if (isDomainError(error)) {
      // Domain errors have specific user-friendly messages
      return error.message;
    }

    // Generic error messages based on error type
    if (error.message.includes('network') || error.message.includes('fetch')) {
      return 'Network connection issue. Please check your internet connection and try again.';
    }

    if (error.message.includes('timeout')) {
      return 'The operation timed out. Please try again.';
    }

    if (error.message.includes('unauthorized') || error.message.includes('forbidden')) {
      return 'You do not have permission to perform this action.';
    }

    // Default generic message
    return 'An unexpected error occurred. Please try again or contact support if the problem persists.';
  }

  shouldRetry(error: Error): boolean {
    // Don't retry domain errors
    if (isDomainError(error)) {
      return false;
    }

    // Retry network and timeout errors
    return error.message.includes('network') ||
           error.message.includes('timeout') ||
           error.message.includes('fetch');
  }

  /**
   * Create error boundary for React components
   * Note: This returns a class that should be used as a React component
   * The actual JSX rendering should be handled by the presentation layer
   */
  createErrorBoundary() {
    const logger = this.logger;
    return class ErrorBoundary extends React.Component<
      { children: React.ReactNode; onError?: (error: Error) => void },
      { hasError: boolean; error?: Error }
    > {
      constructor(props: any) {
        super(props);
        this.state = { hasError: false };
      }

      static getDerivedStateFromError(error: Error) {
        return { hasError: true, error };
      }

      componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
        logger.error('React Error Boundary caught an error', error, {
          componentStack: errorInfo.componentStack
        });

        if (this.props.onError) {
          this.props.onError(error);
        }
      }

      render() {
        if (this.state.hasError && this.state.error) {
          // Return a simple error message - actual JSX rendering
          // should be handled by the presentation layer
          return React.createElement('div', {
            style: {
              padding: '20px',
              margin: '20px',
              border: '1px solid #ff6b6b',
              borderRadius: '4px',
              backgroundColor: '#ffeaea'
            }
          },
            React.createElement('h3', null, 'Something went wrong'),
            React.createElement('p', null, this.state.error.message),
            React.createElement('button', {
              onClick: () => window.location.reload()
            }, 'Reload Page')
          );
        }

        return this.props.children;
      }
    };
  }
}

// Import React for error boundary (lazy import to avoid circular dependencies)
import React from 'react';
