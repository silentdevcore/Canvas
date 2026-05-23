/**
 * Structured logging system for template operations
 */

export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
  FATAL = 4
}

export interface LogEntry {
  timestamp: string;
  level: LogLevel;
  category: string;
  message: string;
  data?: any;
  traceId?: string;
  userId?: string;
  templateId?: string;
  duration?: number;
  error?: Error;
}

export interface LogTransport {
  log(entry: LogEntry): void;
}

export class ConsoleTransport implements LogTransport {
  log(entry: LogEntry): void {
    const levelName = LogLevel[entry.level];
    const timestamp = new Date(entry.timestamp).toISOString();
    const prefix = `[${timestamp}] ${levelName} [${entry.category}]`;

    switch (entry.level) {
      case LogLevel.DEBUG:
        console.debug(prefix, entry.message, entry.data);
        break;
      case LogLevel.INFO:
        console.info(prefix, entry.message, entry.data);
        break;
      case LogLevel.WARN:
        console.warn(prefix, entry.message, entry.data);
        break;
      case LogLevel.ERROR:
      case LogLevel.FATAL:
        console.error(prefix, entry.message, entry.error || entry.data);
        break;
    }
  }
}

export class MemoryTransport implements LogTransport {
  private logs: LogEntry[] = [];
  private maxSize = 1000;

  log(entry: LogEntry): void {
    this.logs.push(entry);
    if (this.logs.length > this.maxSize) {
      this.logs.shift(); // Remove oldest
    }
  }

  getLogs(level?: LogLevel, category?: string): LogEntry[] {
    return this.logs.filter(log => {
      if (level !== undefined && log.level < level) return false;
      if (category && log.category !== category) return false;
      return true;
    });
  }

  clear(): void {
    this.logs = [];
  }
}

export class Logger {
  private transports: LogTransport[] = [new ConsoleTransport()];
  private context: Partial<LogEntry> = {};

  constructor(private category: string) {}

  setContext(context: Partial<LogEntry>): void {
    this.context = { ...this.context, ...context };
  }

  clearContext(): void {
    this.context = {};
  }

  addTransport(transport: LogTransport): void {
    this.transports.push(transport);
  }

  removeTransport(transport: LogTransport): void {
    this.transports = this.transports.filter(t => t !== transport);
  }

  debug(message: string, data?: any): void {
    this.log(LogLevel.DEBUG, message, data);
  }

  info(message: string, data?: any): void {
    this.log(LogLevel.INFO, message, data);
  }

  warn(message: string, data?: any): void {
    this.log(LogLevel.WARN, message, data);
  }

  error(message: string, error?: Error, data?: any): void {
    this.log(LogLevel.ERROR, message, data, error);
  }

  fatal(message: string, error?: Error, data?: any): void {
    this.log(LogLevel.FATAL, message, data, error);
  }

  /**
   * Create a child logger with additional context
   */
  child(additionalContext: Partial<LogEntry>): Logger {
    const childLogger = new Logger(this.category);
    childLogger.setContext({ ...this.context, ...additionalContext });
    this.transports.forEach(transport => childLogger.addTransport(transport));
    return childLogger;
  }

  /**
   * Time a function execution and log the duration
   */
  async time<T>(
    label: string,
    fn: () => Promise<T>,
    level: LogLevel = LogLevel.INFO
  ): Promise<T> {
    const start = Date.now();
    try {
      const result = await fn();
      const duration = Date.now() - start;
      this.log(level, `${label} completed`, { duration });
      return result;
    } catch (error) {
      const duration = Date.now() - start;
      this.log(LogLevel.ERROR, `${label} failed`, { duration }, error as Error);
      throw error;
    }
  }

  /**
   * Time a synchronous function execution and log the duration
   */
  timeSync<T>(
    label: string,
    fn: () => T,
    level: LogLevel = LogLevel.INFO
  ): T {
    const start = Date.now();
    try {
      const result = fn();
      const duration = Date.now() - start;
      this.log(level, `${label} completed`, { duration });
      return result;
    } catch (error) {
      const duration = Date.now() - start;
      this.log(LogLevel.ERROR, `${label} failed`, { duration }, error as Error);
      throw error;
    }
  }

  private log(level: LogLevel, message: string, data?: any, error?: Error): void {
    const entry: LogEntry = {
      timestamp: new Date().toISOString(),
      level,
      category: this.category,
      message,
      data,
      error,
      ...this.context
    };

    this.transports.forEach(transport => {
      try {
        transport.log(entry);
      } catch (transportError) {
        // Prevent transport errors from breaking the application
        console.error('Logging transport error:', transportError);
      }
    });
  }
}

// Global logger instances
export const templateLogger = new Logger('template');
export const renderLogger = new Logger('render');
export const apiLogger = new Logger('api');
export const cacheLogger = new Logger('cache');

// Memory transport for testing/debugging
export const memoryTransport = new MemoryTransport();
templateLogger.addTransport(memoryTransport);
renderLogger.addTransport(memoryTransport);
apiLogger.addTransport(memoryTransport);
cacheLogger.addTransport(memoryTransport);

// Utility functions
export function generateTraceId(): string {
  return Math.random().toString(36).substring(2, 15) +
         Math.random().toString(36).substring(2, 15);
}

export function sanitizeLogData(data: any): any {
  if (!data) return data;

  const sensitiveKeys = ['password', 'token', 'secret', 'key', 'apikey'];
  const sanitized = { ...data };

  for (const key of Object.keys(sanitized)) {
    if (sensitiveKeys.some(sensitive => key.toLowerCase().includes(sensitive))) {
      sanitized[key] = '[REDACTED]';
    } else if (typeof sanitized[key] === 'object') {
      sanitized[key] = sanitizeLogData(sanitized[key]);
    }
  }

  return sanitized;
}