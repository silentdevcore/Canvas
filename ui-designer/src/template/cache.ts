/**
 * Template compilation and rendering cache for performance optimization
 */

export interface CacheEntry<T> {
  value: T;
  timestamp: number;
  ttl: number;
  hits: number;
}

export interface CacheStats {
  hits: number;
  misses: number;
  evictions: number;
  size: number;
  hitRate: number;
}

export class TemplateCache {
  protected cache = new Map<string, CacheEntry<any>>();
  private stats = {
    hits: 0,
    misses: 0,
    evictions: 0,
    size: 0,
    hitRate: 0
  };

  constructor(
    private maxSize: number = 100,
    protected defaultTTL: number = 300000 // 5 minutes
  ) {}

  /**
   * Get cached value with automatic expiration check
   */
  get<T>(key: string): T | null {
    const entry = this.cache.get(key);

    if (!entry) {
      this.stats.misses++;
      this.updateHitRate();
      return null;
    }

    // Check if expired
    if (Date.now() - entry.timestamp > entry.ttl) {
      this.cache.delete(key);
      this.stats.evictions++;
      this.stats.size--;
      this.stats.misses++;
      this.updateHitRate();
      return null;
    }

    entry.hits++;
    this.stats.hits++;
    this.updateHitRate();
    return entry.value;
  }

  /**
   * Set cached value with TTL
   */
  set<T>(key: string, value: T, ttl?: number): void {
    // Evict if at capacity
    if (this.cache.size >= this.maxSize && !this.cache.has(key)) {
      // Simple LRU: remove oldest entry
      const oldestKey = Array.from(this.cache.entries())
        .sort(([,a], [,b]) => a.timestamp - b.timestamp)[0]?.[0];

      if (oldestKey) {
        this.cache.delete(oldestKey);
        this.stats.evictions++;
        this.stats.size--;
      }
    }

    const entry: CacheEntry<T> = {
      value,
      timestamp: Date.now(),
      ttl: ttl || this.defaultTTL,
      hits: 0
    };

    const isNew = !this.cache.has(key);
    this.cache.set(key, entry);

    if (isNew) {
      this.stats.size++;
    }
  }

  /**
   * Delete cached value
   */
  delete(key: string): boolean {
    const deleted = this.cache.delete(key);
    if (deleted) {
      this.stats.size--;
    }
    return deleted;
  }

  /**
   * Clear all cached values
   */
  clear(): void {
    this.cache.clear();
    this.stats.size = 0;
    this.stats.evictions += this.stats.size;
  }

  /**
   * Get cache statistics
   */
  getStats(): CacheStats {
    return { ...this.stats };
  }

  /**
   * Clean expired entries
   */
  cleanup(): number {
    const now = Date.now();
    let cleaned = 0;

    for (const [key, entry] of this.cache.entries()) {
      if (now - entry.timestamp > entry.ttl) {
        this.cache.delete(key);
        this.stats.evictions++;
        this.stats.size--;
        cleaned++;
      }
    }

    return cleaned;
  }

  private updateHitRate(): void {
    const total = this.stats.hits + this.stats.misses;
    this.stats.hitRate = total > 0 ? this.stats.hits / total : 0;
  }
}

/**
 * Asset cache for images and other resources
 */
export class AssetCache extends TemplateCache {
  constructor(maxSize: number = 50) {
    super(maxSize, 1800000); // 30 minutes TTL for assets
  }

  /**
   * Cache asset with content-based key
   */
  setAsset(url: string, data: ArrayBuffer | string, contentType?: string): void {
    const key = this.generateAssetKey(url, data);
    const size = typeof data === 'string' ? data.length : data.byteLength;
    const metadata = { url, contentType, size };
    this.set(key, { data, metadata }, this.defaultTTL);
  }

  /**
   * Get cached asset
   */
  getAsset(url: string, data?: ArrayBuffer | string): { data: ArrayBuffer | string; metadata: any } | null {
    const key = data ? this.generateAssetKey(url, data) : this.generateUrlKey(url);
    return this.get(key);
  }

  private generateAssetKey(url: string, data: ArrayBuffer | string): string {
    // Use content hash for cache key
    const hash = this.simpleHash(data);
    return `asset:${url}:${hash}`;
  }

  private generateUrlKey(url: string): string {
    return `asset:${url}`;
  }

  private simpleHash(data: ArrayBuffer | string): string {
    let hash = 0;
    const str = typeof data === 'string' ? data : new Uint8Array(data).reduce((s, b) => s + String.fromCharCode(b), '');
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32-bit integer
    }
    return Math.abs(hash).toString(36);
  }
}

/**
 * Template compilation cache
 */
export class TemplateCompilationCache extends TemplateCache {
  constructor(maxSize: number = 200) {
    super(maxSize, 600000); // 10 minutes TTL for compiled templates
  }

  /**
   * Cache compiled template
   */
  setCompiled(templateId: string, version: string, compiledTemplate: any): void {
    const key = `compiled:${templateId}:${version}`;
    this.set(key, compiledTemplate, this.defaultTTL);
  }

  /**
   * Get compiled template
   */
  getCompiled(templateId: string, version: string): any | null {
    const key = `compiled:${templateId}:${version}`;
    return this.get(key);
  }

  /**
   * Invalidate all versions of a template
   */
  invalidateTemplate(templateId: string): number {
    let invalidated = 0;
    for (const key of this.cache.keys()) {
      if (key.startsWith(`compiled:${templateId}:`)) {
        this.delete(key);
        invalidated++;
      }
    }
    return invalidated;
  }
}

// Global cache instances
export const templateCache = new TemplateCompilationCache();
export const assetCache = new AssetCache();

// Periodic cleanup
setInterval(() => {
  templateCache.cleanup();
  assetCache.cleanup();
}, 300000); // Clean every 5 minutes