/**
 * Domain-specific error types following Clean Architecture principles.
 * These errors represent business rule violations and domain constraints.
 */

export abstract class DomainError extends Error {
  abstract readonly code: string;

  constructor(message: string) {
    super(message);
    this.name = this.constructor.name;
  }
}

export class ValidationError extends DomainError {
  readonly code = 'VALIDATION_ERROR';

  constructor(
    message: string,
    public readonly field?: string,
    public readonly value?: any
  ) {
    super(message);
  }
}

export class NotFoundError extends DomainError {
  readonly code = 'NOT_FOUND_ERROR';

  constructor(
    message: string,
    public readonly resource: string,
    public readonly id?: string
  ) {
    super(message);
  }
}

export class BusinessRuleViolationError extends DomainError {
  readonly code = 'BUSINESS_RULE_VIOLATION';

  constructor(
    message: string,
    public readonly rule: string,
    public readonly context?: Record<string, any>
  ) {
    super(message);
  }
}

export class ConcurrencyError extends DomainError {
  readonly code = 'CONCURRENCY_ERROR';

  constructor(
    message: string,
    public readonly resource: string,
    public readonly expectedVersion?: number,
    public readonly actualVersion?: number
  ) {
    super(message);
  }
}

export class AuthorizationError extends DomainError {
  readonly code = 'AUTHORIZATION_ERROR';

  constructor(
    message: string,
    public readonly action: string,
    public readonly resource?: string
  ) {
    super(message);
  }
}

/**
 * Type guard functions for error type checking
 */
export const isDomainError = (error: any): error is DomainError => {
  return error instanceof DomainError;
};

export const isValidationError = (error: any): error is ValidationError => {
  return error instanceof ValidationError;
};

export const isNotFoundError = (error: any): error is NotFoundError => {
  return error instanceof NotFoundError;
};

export const isBusinessRuleViolationError = (error: any): error is BusinessRuleViolationError => {
  return error instanceof BusinessRuleViolationError;
};

export const isConcurrencyError = (error: any): error is ConcurrencyError => {
  return error instanceof ConcurrencyError;
};

export const isAuthorizationError = (error: any): error is AuthorizationError => {
  return error instanceof AuthorizationError;
};