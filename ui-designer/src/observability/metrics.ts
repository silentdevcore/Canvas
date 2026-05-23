/**
 * Metrics collection system for performance monitoring
 */

export interface MetricValue {
  name: string;
  value: number;
  timestamp: number;
  tags?: Record<string, string>;
}

export interface Counter extends MetricValue {
  type: 'counter';
}

export interface Gauge extends MetricValue {
  type: 'gauge';
}

export interface Histogram {
  name: string;
  count: number;
  sum: number;
  min: number;
  max: number;
  p50: number;
  p95: number;
  p99: number;
  timestamp: number;
  tags?: Record<string, string>;
}

export interface MetricsCollector {
  increment(name: string, value?: number, tags?: Record<string, string>): void;
  gauge(name: string, value: number, tags?: Record<string, string>): void;
  timing(name: string, duration: number, tags?: Record<string, string>): void;
  getMetrics(): { counters: Counter[]; gauges: Gauge[]; histograms: Histogram[] };
  reset(): void;
}

export class InMemoryMetricsCollector implements MetricsCollector {
  private counters = new Map<string, Counter>();
  private gauges = new Map<string, Gauge>();
  private histograms = new Map<string, Histogram>();

  increment(name: string, value: number = 1, tags?: Record<string, string>): void {
    const key = this.getKey(name, tags);
    const existing = this.counters.get(key);

    if (existing) {
      existing.value += value;
      existing.timestamp = Date.now();
    } else {
      this.counters.set(key, {
        type: 'counter',
        name,
        value,
        timestamp: Date.now(),
        tags
      });
    }
  }

  gauge(name: string, value: number, tags?: Record<string, string>): void {
    const key = this.getKey(name, tags);
    this.gauges.set(key, {
      type: 'gauge',
      name,
      value,
      timestamp: Date.now(),
      tags
    });
  }

  timing(name: string, duration: number, tags?: Record<string, string>): void {
    const key = this.getKey(name, tags);
    const existing = this.histograms.get(key);

    if (existing) {
      existing.count++;
      existing.sum += duration;
      existing.min = Math.min(existing.min, duration);
      existing.max = Math.max(existing.max, duration);
      existing.timestamp = Date.now();

      // Update percentiles (simplified calculation)
      this.updatePercentiles(existing, duration);
    } else {
      this.histograms.set(key, {
        name,
        count: 1,
        sum: duration,
        min: duration,
        max: duration,
        p50: duration,
        p95: duration,
        p99: duration,
        timestamp: Date.now(),
        tags
      });
    }
  }

  getMetrics() {
    return {
      counters: Array.from(this.counters.values()),
      gauges: Array.from(this.gauges.values()),
      histograms: Array.from(this.histograms.values())
    };
  }

  reset(): void {
    this.counters.clear();
    this.gauges.clear();
    this.histograms.clear();
  }

  private getKey(name: string, tags?: Record<string, string>): string {
    if (!tags) return name;
    const sortedTags = Object.keys(tags).sort().map(key => `${key}=${tags[key]}`).join(',');
    return `${name}{${sortedTags}}`;
  }

  private updatePercentiles(histogram: Histogram, newValue: number): void {
    // Simplified percentile calculation - in production, you'd use a proper algorithm
    const values = [histogram.p50, histogram.p95, histogram.p99, newValue].sort((a, b) => a - b);
    histogram.p50 = values[Math.floor(values.length * 0.5)];
    histogram.p95 = values[Math.floor(values.length * 0.95)];
    histogram.p99 = values[Math.floor(values.length * 0.99)];
  }
}

/**
 * Performance monitoring utilities
 */
export class PerformanceMonitor {
  private static instance: PerformanceMonitor;
  private collector: MetricsCollector;

  private constructor() {
    this.collector = new InMemoryMetricsCollector();
  }

  static getInstance(): PerformanceMonitor {
    if (!PerformanceMonitor.instance) {
      PerformanceMonitor.instance = new PerformanceMonitor();
    }
    return PerformanceMonitor.instance;
  }

  setCollector(collector: MetricsCollector): void {
    this.collector = collector;
  }

  /**
   * Time a function execution and record metrics
   */
  async timeAsync<T>(
    name: string,
    fn: () => Promise<T>,
    tags?: Record<string, string>
  ): Promise<T> {
    const start = performance.now();
    try {
      const result = await fn();
      const duration = performance.now() - start;
      this.collector.timing(name, duration, tags);
      return result;
    } catch (error) {
      const duration = performance.now() - start;
      this.collector.timing(`${name}_error`, duration, tags);
      throw error;
    }
  }

  /**
   * Time a synchronous function execution and record metrics
   */
  timeSync<T>(
    name: string,
    fn: () => T,
    tags?: Record<string, string>
  ): T {
    const start = performance.now();
    try {
      const result = fn();
      const duration = performance.now() - start;
      this.collector.timing(name, duration, tags);
      return result;
    } catch (error) {
      const duration = performance.now() - start;
      this.collector.timing(`${name}_error`, duration, tags);
      throw error;
    }
  }

  /**
   * Record a counter metric
   */
  increment(name: string, value?: number, tags?: Record<string, string>): void {
    this.collector.increment(name, value, tags);
  }

  /**
   * Record a gauge metric
   */
  gauge(name: string, value: number, tags?: Record<string, string>): void {
    this.collector.gauge(name, value, tags);
  }

  /**
   * Record a timing metric
   */
  timing(name: string, duration: number, tags?: Record<string, string>): void {
    this.collector.timing(name, duration, tags);
  }

  /**
   * Get current metrics
   */
  getMetrics() {
    return this.collector.getMetrics();
  }

  /**
   * Reset all metrics
   */
  reset(): void {
    this.collector.reset();
  }
}

/**
 * Template-specific metrics
 */
export class TemplateMetrics {
  private monitor = PerformanceMonitor.getInstance();

  recordTemplateLoad(templateId: string, duration: number, success: boolean): void {
    this.monitor.timing('template.load', duration, {
      templateId,
      success: success.toString()
    });
  }

  recordTemplateSave(templateId: string, duration: number, success: boolean): void {
    this.monitor.timing('template.save', duration, {
      templateId,
      success: success.toString()
    });
  }

  recordTemplateRender(templateId: string, duration: number, success: boolean, pageCount?: number): void {
    const tags: Record<string, string> = {
      templateId,
      success: success.toString()
    };
    if (pageCount !== undefined) {
      tags.pageCount = pageCount.toString();
    }
    this.monitor.timing('template.render', duration, tags);
  }

  recordExpressionEvaluation(success: boolean, duration?: number): void {
    if (duration !== undefined) {
      this.monitor.timing('expression.evaluate', duration, {
        success: success.toString()
      });
    }
    this.monitor.increment('expression.total', 1, {
      success: success.toString()
    });
  }

  recordCacheHit(cacheType: string, hit: boolean): void {
    this.monitor.increment('cache.access', 1, {
      type: cacheType,
      hit: hit.toString()
    });
  }

  recordError(category: string, errorType: string): void {
    this.monitor.increment('error.total', 1, {
      category,
      type: errorType
    });
  }

  recordApiCall(endpoint: string, method: string, statusCode: number, duration: number): void {
    this.monitor.timing('api.call', duration, {
      endpoint,
      method,
      statusCode: statusCode.toString(),
      statusClass: Math.floor(statusCode / 100) + 'xx'
    });
  }
}

/**
 * Health check utilities
 */
export class HealthChecker {
  private monitor = PerformanceMonitor.getInstance();

  async checkDatabase(): Promise<{ healthy: boolean; latency: number }> {
    const start = performance.now();
    try {
      // Simulate database check - in real implementation, check actual DB connection
      await new Promise(resolve => setTimeout(resolve, Math.random() * 10));
      const latency = performance.now() - start;
      this.monitor.gauge('health.database.latency', latency);
      return { healthy: true, latency };
    } catch (error) {
      const latency = performance.now() - start;
      this.monitor.gauge('health.database.latency', latency);
      return { healthy: false, latency };
    }
  }

  async checkCache(): Promise<{ healthy: boolean; size: number }> {
    try {
      // Check cache health
      const metrics = this.monitor.getMetrics();
      const cacheSize = metrics.gauges.find(g => g.name.includes('cache.size'))?.value || 0;
      return { healthy: true, size: cacheSize };
    } catch (error) {
      return { healthy: false, size: 0 };
    }
  }

  getSystemHealth(): {
    status: 'healthy' | 'degraded' | 'unhealthy';
    checks: Record<string, any>;
    timestamp: string;
  } {
    const metrics = this.monitor.getMetrics();
    const checks = {
      database: { status: 'unknown' },
      cache: { status: 'unknown' },
      memory: { status: 'healthy' },
      cpu: { status: 'healthy' }
    };

    // Analyze metrics to determine health
    const errorRate = metrics.counters
      .filter(c => c.name.includes('error'))
      .reduce((sum, c) => sum + c.value, 0);

    const totalRequests = metrics.counters
      .filter(c => c.name.includes('api.call'))
      .reduce((sum, c) => sum + c.value, 0);

    const avgResponseTime = metrics.histograms
      .find(h => h.name === 'api.call')?.p95 || 0;

    let status: 'healthy' | 'degraded' | 'unhealthy' = 'healthy';

    if (errorRate > totalRequests * 0.1) { // >10% error rate
      status = 'unhealthy';
    } else if (avgResponseTime > 5000) { // >5s average response time
      status = 'degraded';
    }

    return {
      status,
      checks,
      timestamp: new Date().toISOString()
    };
  }
}

// Global instances
export const metrics = new TemplateMetrics();
export const healthChecker = new HealthChecker();
export const performanceMonitor = PerformanceMonitor.getInstance();