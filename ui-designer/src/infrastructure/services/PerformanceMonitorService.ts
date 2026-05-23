/**
 * Performance monitoring service following Clean Architecture principles.
 * Provides performance tracking, metrics collection, and optimization insights.
 */

import { ILoggerService } from './LoggerService';

export interface PerformanceMetric {
  name: string;
  value: number;
  unit: 'ms' | 'bytes' | 'count' | 'percentage';
  timestamp: Date;
  context?: Record<string, any>;
}

export interface PerformanceThreshold {
  metric: string;
  warning: number;
  critical: number;
  unit: 'ms' | 'bytes' | 'count' | 'percentage';
}

export interface IPerformanceMonitorService {
  startMeasurement(name: string, context?: Record<string, any>): () => void;
  measureExecutionTime<T>(
    name: string,
    fn: () => T,
    context?: Record<string, any>
  ): T;
  measureAsyncExecutionTime<T>(
    name: string,
    fn: () => Promise<T>,
    context?: Record<string, any>
  ): Promise<T>;
  recordMetric(metric: Omit<PerformanceMetric, 'timestamp'>): void;
  getMetrics(name?: string): PerformanceMetric[];
  getAverageMetric(name: string): number | null;
  checkThresholds(): { warnings: string[]; critical: string[] };
  clearMetrics(): void;
}

/**
 * Performance monitor implementation with memory-efficient storage
 */
export class PerformanceMonitorService implements IPerformanceMonitorService {
  private metrics: Map<string, PerformanceMetric[]> = new Map();
  private activeMeasurements: Map<string, { startTime: number; context?: Record<string, any> }> = new Map();
  private thresholds: PerformanceThreshold[] = [];
  private maxMetricsPerType = 1000; // Prevent memory leaks
  private logger: ILoggerService;

  constructor(logger: ILoggerService, thresholds: PerformanceThreshold[] = []) {
    this.logger = logger;
    this.thresholds = thresholds;
    this.initializeDefaultThresholds();
  }

  private initializeDefaultThresholds(): void {
    // Add default performance thresholds
    this.thresholds.push(
      { metric: 'useCase.executionTime', warning: 100, critical: 500, unit: 'ms' },
      { metric: 'repository.operationTime', warning: 50, critical: 200, unit: 'ms' },
      { metric: 'render.componentTime', warning: 16, critical: 100, unit: 'ms' }, // 16ms = 60fps
      { metric: 'memory.heapUsed', warning: 50 * 1024 * 1024, critical: 100 * 1024 * 1024, unit: 'bytes' } // 50MB/100MB
    );
  }

  startMeasurement(name: string, context?: Record<string, any>): () => void {
    const id = `${name}_${Date.now()}_${Math.random()}`;
    this.activeMeasurements.set(id, {
      startTime: performance.now(),
      context
    });

    return () => {
      const measurement = this.activeMeasurements.get(id);
      if (measurement) {
        const duration = performance.now() - measurement.startTime;
        this.recordMetric({
          name,
          value: duration,
          unit: 'ms',
          context: measurement.context
        });
        this.activeMeasurements.delete(id);
      }
    };
  }

  measureExecutionTime<T>(
    name: string,
    fn: () => T,
    context?: Record<string, any>
  ): T {
    const endMeasurement = this.startMeasurement(name, context);
    try {
      const result = fn();
      endMeasurement();
      return result;
    } catch (error) {
      endMeasurement();
      throw error;
    }
  }

  async measureAsyncExecutionTime<T>(
    name: string,
    fn: () => Promise<T>,
    context?: Record<string, any>
  ): Promise<T> {
    const endMeasurement = this.startMeasurement(name, context);
    try {
      const result = await fn();
      endMeasurement();
      return result;
    } catch (error) {
      endMeasurement();
      throw error;
    }
  }

  recordMetric(metric: Omit<PerformanceMetric, 'timestamp'>): void {
    const fullMetric: PerformanceMetric = {
      ...metric,
      timestamp: new Date()
    };

    if (!this.metrics.has(metric.name)) {
      this.metrics.set(metric.name, []);
    }

    const metricsList = this.metrics.get(metric.name)!;
    metricsList.push(fullMetric);

    // Maintain max metrics limit
    if (metricsList.length > this.maxMetricsPerType) {
      metricsList.shift(); // Remove oldest
    }

    // Log performance issues
    this.checkAndLogThresholds(fullMetric);
  }

  private checkAndLogThresholds(metric: PerformanceMetric): void {
    const threshold = this.thresholds.find(t => t.metric === metric.name);
    if (!threshold) return;

    if (metric.value >= threshold.critical) {
      this.logger.error(`Critical performance threshold exceeded: ${metric.name}`, undefined, {
        value: metric.value,
        threshold: threshold.critical,
        unit: metric.unit,
        context: metric.context
      });
    } else if (metric.value >= threshold.warning) {
      this.logger.warn(`Performance threshold warning: ${metric.name}`, {
        value: metric.value,
        threshold: threshold.warning,
        unit: metric.unit,
        context: metric.context
      });
    }
  }

  getMetrics(name?: string): PerformanceMetric[] {
    if (name) {
      return this.metrics.get(name) || [];
    }

    const allMetrics: PerformanceMetric[] = [];
    for (const metricsList of this.metrics.values()) {
      allMetrics.push(...metricsList);
    }
    return allMetrics.sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());
  }

  getAverageMetric(name: string): number | null {
    const metrics = this.metrics.get(name);
    if (!metrics || metrics.length === 0) return null;

    const sum = metrics.reduce((acc, metric) => acc + metric.value, 0);
    return sum / metrics.length;
  }

  checkThresholds(): { warnings: string[]; critical: string[] } {
    const warnings: string[] = [];
    const critical: string[] = [];

    for (const [metricName, metrics] of this.metrics.entries()) {
      if (metrics.length === 0) continue;

      const threshold = this.thresholds.find(t => t.metric === metricName);
      if (!threshold) continue;

      const latestMetric = metrics[metrics.length - 1];

      if (latestMetric.value >= threshold.critical) {
        critical.push(`${metricName}: ${latestMetric.value}${threshold.unit} (threshold: ${threshold.critical}${threshold.unit})`);
      } else if (latestMetric.value >= threshold.warning) {
        warnings.push(`${metricName}: ${latestMetric.value}${threshold.unit} (threshold: ${threshold.warning}${threshold.unit})`);
      }
    }

    return { warnings, critical };
  }

  clearMetrics(): void {
    this.metrics.clear();
    this.activeMeasurements.clear();
  }

  /**
   * Memory usage monitoring
   */
  recordMemoryUsage(): void {
    if ('memory' in performance) {
      const memory = (performance as any).memory;
      this.recordMetric({
        name: 'memory.heapUsed',
        value: memory.usedJSHeapSize,
        unit: 'bytes'
      });
      this.recordMetric({
        name: 'memory.heapTotal',
        value: memory.totalJSHeapSize,
        unit: 'bytes'
      });
      this.recordMetric({
        name: 'memory.heapLimit',
        value: memory.jsHeapSizeLimit,
        unit: 'bytes'
      });
    }
  }

  /**
   * Component render time tracking
   */
  trackComponentRender(componentName: string): () => void {
    return this.startMeasurement(`render.componentTime`, { component: componentName });
  }

  /**
   * Use case execution time tracking
   */
  trackUseCaseExecution(useCaseName: string): () => void {
    return this.startMeasurement(`useCase.executionTime`, { useCase: useCaseName });
  }

  /**
   * Repository operation time tracking
   */
  trackRepositoryOperation(repositoryName: string, operation: string): () => void {
    return this.startMeasurement(`repository.operationTime`, {
      repository: repositoryName,
      operation
    });
  }

  /**
   * Generate performance report
   */
  generateReport(): {
    summary: Record<string, { count: number; avg: number; min: number; max: number; unit: string }>;
    thresholds: { warnings: string[]; critical: string[] };
    recommendations: string[];
  } {
    const summary: Record<string, { count: number; avg: number; min: number; max: number; unit: string }> = {};
    const recommendations: string[] = [];

    for (const [metricName, metrics] of this.metrics.entries()) {
      if (metrics.length === 0) continue;

      const values = metrics.map(m => m.value);
      const avg = values.reduce((a, b) => a + b, 0) / values.length;
      const min = Math.min(...values);
      const max = Math.max(...values);
      const unit = metrics[0].unit;

      summary[metricName] = {
        count: metrics.length,
        avg: Math.round(avg * 100) / 100,
        min: Math.round(min * 100) / 100,
        max: Math.round(max * 100) / 100,
        unit
      };

      // Generate recommendations based on metrics
      if (metricName.includes('render.componentTime') && avg > 16) {
        recommendations.push(`Consider optimizing ${metricName} (avg: ${avg}ms) - target 16ms for 60fps`);
      }
      if (metricName.includes('useCase.executionTime') && avg > 100) {
        recommendations.push(`Use case ${metricName} is slow (avg: ${avg}ms) - consider optimization`);
      }
    }

    const thresholds = this.checkThresholds();

    return {
      summary,
      thresholds,
      recommendations
    };
  }
}