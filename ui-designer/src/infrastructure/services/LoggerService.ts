/**
 * Logging service following Clean Architecture principles.
 * Provides structured logging with different levels and contexts.
 */

export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
  FATAL = 4
}

export interface LogEntry {
  timestamp: Date;
  level: LogLevel;
  message: string;
  context?: Record<string, any>;
  error?: Error;
  userId?: string;
  sessionId?: string;
  correlationId?: string;
}

export interface ILoggerService {
  debug(message: string, context?: Record<string, any>): void;
  info(message: string, context?: Record<string, any>): void;
  warn(message: string, context?: Record<string, any>): void;
  error(message: string, error?: Error, context?: Record<string, any>): void;
  fatal(message: string, error?: Error, context?: Record<string, any>): void;
  setCorrelationId(id: string): void;
  setUserId(id: string): void;
  setSessionId(id: string): void;
}

/**
 * Console-based logger implementation.
 * In production, this could be replaced with services like DataDog, LogRocket, etc.
 */
export class ConsoleLoggerService implements ILoggerService {
  private correlationId?: string;
  private userId?: string;
  private sessionId?: string;
  private minLevel: LogLevel = LogLevel.INFO;

  constructor(minLevel: LogLevel = LogLevel.INFO) {
    this.minLevel = minLevel;
  }

  setCorrelationId(id: string): void {
    this.correlationId = id;
  }

  setUserId(id: string): void {
    this.userId = id;
  }

  setSessionId(id: string): void {
    this.sessionId = id;
  }

  debug(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.DEBUG, message, undefined, context);
  }

  info(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.INFO, message, undefined, context);
  }

  warn(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.WARN, message, undefined, context);
  }

  error(message: string, error?: Error, context?: Record<string, any>): void {
    this.log(LogLevel.ERROR, message, error, context);
  }

  fatal(message: string, error?: Error, context?: Record<string, any>): void {
    this.log(LogLevel.FATAL, message, error, context);
  }

  private log(
    level: LogLevel,
    message: string,
    error?: Error,
    context?: Record<string, any>
  ): void {
    if (level < this.minLevel) {
      return;
    }

    const entry: LogEntry = {
      timestamp: new Date(),
      level,
      message,
      context,
      error,
      userId: this.userId,
      sessionId: this.sessionId,
      correlationId: this.correlationId
    };

    this.writeToConsole(entry);
  }

  private writeToConsole(entry: LogEntry): void {
    const levelName = LogLevel[entry.level];
    const timestamp = entry.timestamp.toISOString();
    const prefix = `[${timestamp}] ${levelName}`;

    const logData = {
      message: entry.message,
      context: entry.context,
      userId: entry.userId,
      sessionId: entry.sessionId,
      correlationId: entry.correlationId,
      error: entry.error?.stack
    };

    switch (entry.level) {
      case LogLevel.DEBUG:
        console.debug(prefix, logData);
        break;
      case LogLevel.INFO:
        console.info(prefix, logData);
        break;
      case LogLevel.WARN:
        console.warn(prefix, logData);
        break;
      case LogLevel.ERROR:
      case LogLevel.FATAL:
        console.error(prefix, logData);
        break;
    }
  }
}

/**
 * No-op logger for testing or when logging is disabled
 */
export class NoOpLoggerService implements ILoggerService {
  debug(): void {}
  info(): void {}
  warn(): void {}
  error(): void {}
  fatal(): void {}
  setCorrelationId(): void {}
  setUserId(): void {}
  setSessionId(): void {}
}

/**
 * Performance monitoring logger
 */
export class PerformanceLoggerService implements ILoggerService {
  private logger: ILoggerService;

  constructor(logger: ILoggerService) {
    this.logger = logger;
  }

  debug(message: string, context?: Record<string, any>): void {
    this.logger.debug(message, { ...context, source: 'performance' });
  }

  info(message: string, context?: Record<string, any>): void {
    this.logger.info(message, { ...context, source: 'performance' });
  }

  warn(message: string, context?: Record<string, any>): void {
    this.logger.warn(message, { ...context, source: 'performance' });
  }

  error(message: string, error?: Error, context?: Record<string, any>): void {
    this.logger.error(message, error, { ...context, source: 'performance' });
  }

  fatal(message: string, error?: Error, context?: Record<string, any>): void {
    this.logger.fatal(message, error, { ...context, source: 'performance' });
  }

  setCorrelationId(id: string): void {
    this.logger.setCorrelationId(id);
  }

  setUserId(id: string): void {
    this.logger.setUserId(id);
  }

  setSessionId(id: string): void {
    this.logger.setSessionId(id);
  }

  /**
   * Measure execution time of a function
   */
  measureExecutionTime<T>(
    operation: string,
    fn: () => T,
    context?: Record<string, any>
  ): T {
    const start = performance.now();
    try {
      const result = fn();
      const duration = performance.now() - start;
      this.info(`Operation completed: ${operation}`, {
        ...context,
        duration: `${duration.toFixed(2)}ms`,
        success: true
      });
      return result;
    } catch (error) {
      const duration = performance.now() - start;
      this.error(`Operation failed: ${operation}`, error as Error, {
        ...context,
        duration: `${duration.toFixed(2)}ms`,
        success: false
      });
      throw error;
    }
  }

  /**
   * Measure async execution time
   */
  async measureAsyncExecutionTime<T>(
    operation: string,
    fn: () => Promise<T>,
    context?: Record<string, any>
  ): Promise<T> {
    const start = performance.now();
    try {
      const result = await fn();
      const duration = performance.now() - start;
      this.info(`Async operation completed: ${operation}`, {
        ...context,
        duration: `${duration.toFixed(2)}ms`,
        success: true
      });
      return result;
    } catch (error) {
      const duration = performance.now() - start;
      this.error(`Async operation failed: ${operation}`, error as Error, {
        ...context,
        duration: `${duration.toFixed(2)}ms`,
        success: false
      });
      throw error;
    }
  }
}