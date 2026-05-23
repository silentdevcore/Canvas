/**
 * Repeat expander for handling collection-based element repetition
 * Enables dynamic generation of repeated elements from data collections
 */

import { evaluateExpression, ExpressionContext } from './expressionEngine';

export interface RepeatConfig {
  repeatSource?: string; // Path to array data (e.g., "order.items")
  itemAlias?: string; // Variable name for current item (default: "item")
  indexAlias?: string; // Variable name for current index (default: "index")
  emptyBehavior?: 'hide' | 'show-placeholder' | 'keep-template'; // What to do when collection is empty
  maxItems?: number; // Maximum number of items to render
  pageBreakBetweenItems?: boolean; // Insert page breaks between items
  rowTemplateMode?: boolean; // Special mode for table rows/lists
}

export interface RepeatInstance {
  item: any; // Current item data
  index: number; // Current index
  context: ExpressionContext; // Full expression context for this instance
  isFirst: boolean;
  isLast: boolean;
  totalCount: number;
}

export interface RepeatResult {
  instances: RepeatInstance[];
  isEmpty: boolean;
  totalCount: number;
  hasMore: boolean; // True if there are more items than maxItems
}

/**
 * Expands a repeat configuration into individual instances
 */
export function expandRepeat(
  config: RepeatConfig,
  baseContext: ExpressionContext,
  safeMode: boolean = true
): RepeatResult {
  const {
    repeatSource,
    itemAlias = 'item',
    indexAlias = 'index',
    emptyBehavior = 'hide',
    maxItems
  } = config;

  // If no repeat source, return empty result
  if (!repeatSource) {
    return {
      instances: [],
      isEmpty: true,
      totalCount: 0,
      hasMore: false
    };
  }

  // Evaluate the repeat source to get the collection
  const sourceResult = evaluateExpression(repeatSource, baseContext, { safeMode });

  if (!sourceResult.isValid || !Array.isArray(sourceResult.value)) {
    // If source is invalid or not an array, handle based on empty behavior
    if (emptyBehavior === 'keep-template') {
      // Return a single instance with null/undefined item
      const context: ExpressionContext = {
        ...baseContext,
        [itemAlias]: undefined,
        [indexAlias]: 0,
        index: 0,
        parent: baseContext.data
      };

      return {
        instances: [{
          item: undefined,
          index: 0,
          context,
          isFirst: true,
          isLast: true,
          totalCount: 0
        }],
        isEmpty: true,
        totalCount: 0,
        hasMore: false
      };
    }

    return {
      instances: [],
      isEmpty: true,
      totalCount: 0,
      hasMore: false
    };
  }

  const collection = sourceResult.value;
  const totalCount = collection.length;

  if (totalCount === 0) {
    // Empty collection
    if (emptyBehavior === 'keep-template') {
      const context: ExpressionContext = {
        ...baseContext,
        [itemAlias]: undefined,
        [indexAlias]: 0,
        index: 0,
        parent: baseContext.data
      };

      return {
        instances: [{
          item: undefined,
          index: 0,
          context,
          isFirst: true,
          isLast: true,
          totalCount: 0
        }],
        isEmpty: true,
        totalCount: 0,
        hasMore: false
      };
    }

    return {
      instances: [],
      isEmpty: true,
      totalCount: 0,
      hasMore: false
    };
  }

  // Generate instances for each item in the collection
  const instances: RepeatInstance[] = [];
  const renderCount = maxItems ? Math.min(totalCount, maxItems) : totalCount;
  const hasMore = maxItems ? totalCount > maxItems : false;

  for (let i = 0; i < renderCount; i++) {
    const item = collection[i];

    // Create context for this instance
    const context: ExpressionContext = {
      ...baseContext,
      [itemAlias]: item,
      [indexAlias]: i,
      index: i,
      parent: baseContext.data
    };

    instances.push({
      item,
      index: i,
      context,
      isFirst: i === 0,
      isLast: i === renderCount - 1,
      totalCount
    });
  }

  return {
    instances,
    isEmpty: false,
    totalCount,
    hasMore
  };
}

/**
 * Validates a repeat configuration
 */
export function validateRepeatConfig(config: RepeatConfig): { isValid: boolean; errors: string[] } {
  const errors: string[] = [];

  if (config.repeatSource) {
    // Validate the repeat source expression
    const validation = { isValid: true, error: undefined }; // Would call validateExpression here
    if (!validation.isValid) {
      errors.push(`Invalid repeat source: ${validation.error}`);
    }
  }

  if (config.maxItems && config.maxItems < 1) {
    errors.push('maxItems must be greater than 0');
  }

  if (config.itemAlias && !/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(config.itemAlias)) {
    errors.push('itemAlias must be a valid JavaScript identifier');
  }

  if (config.indexAlias && !/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(config.indexAlias)) {
    errors.push('indexAlias must be a valid JavaScript identifier');
  }

  return {
    isValid: errors.length === 0,
    errors
  };
}

/**
 * Gets suggested repeat configurations for different element types
 */
export function getRepeatSuggestions(elementType: string): RepeatConfig[] {
  const suggestions: Record<string, RepeatConfig[]> = {
    Table: [
      {
        repeatSource: 'order.items',
        itemAlias: 'item',
        indexAlias: 'index',
        emptyBehavior: 'show-placeholder',
        rowTemplateMode: true
      }
    ],
    List: [
      {
        repeatSource: 'customers',
        itemAlias: 'customer',
        indexAlias: 'i',
        emptyBehavior: 'hide'
      }
    ],
    Grid: [
      {
        repeatSource: 'products',
        itemAlias: 'product',
        indexAlias: 'index',
        emptyBehavior: 'keep-template'
      }
    ],
    Column: [
      {
        repeatSource: 'invoice.lines',
        itemAlias: 'line',
        indexAlias: 'index',
        emptyBehavior: 'hide',
        pageBreakBetweenItems: true
      }
    ]
  };

  return suggestions[elementType] || [];
}

/**
 * Creates a preview of what the repeated elements will look like
 */
export function createRepeatPreview(
  config: RepeatConfig,
  sampleData: Record<string, any>,
  maxPreviewItems: number = 3
): { preview: any[]; totalCount: number; hasMore: boolean } {
  const baseContext: ExpressionContext = {
    data: sampleData,
    element: {},
    index: undefined,
    parent: undefined
  };

  // Temporarily limit maxItems for preview
  const previewConfig = { ...config, maxItems: maxPreviewItems };
  const result = expandRepeat(previewConfig, baseContext);

  return {
    preview: result.instances.map(instance => ({
      index: instance.index,
      item: instance.item,
      isFirst: instance.isFirst,
      isLast: instance.isLast
    })),
    totalCount: result.totalCount,
    hasMore: result.hasMore
  };
}