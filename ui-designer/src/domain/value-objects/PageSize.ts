/**
 * Domain value objects for page sizes and orientations.
 * Immutable value objects with business rules for document page configuration.
 */

export type PageSize = 'A4' | 'A5' | 'A6' | 'Letter' | 'Legal' | 'Custom';
export type PageOrientation = 'Portrait' | 'Landscape';

/**
 * Value object representing page dimensions
 */
export class PageDimensions {
  constructor(
    public readonly width: number,
    public readonly height: number
  ) {
    if (width <= 0 || height <= 0) {
      throw new Error('Page dimensions must be positive');
    }
  }

  /**
   * Creates dimensions for a given size and orientation
   */
  static forSize(size: PageSize, orientation: PageOrientation = 'Portrait'): PageDimensions {
    const dimensions = PAGE_SIZE_DIMENSIONS[size];
    return orientation === 'Landscape'
      ? new PageDimensions(dimensions.height, dimensions.width)
      : new PageDimensions(dimensions.width, dimensions.height);
  }

  /**
   * Gets the aspect ratio (width/height)
   */
  get aspectRatio(): number {
    return this.width / this.height;
  }

  /**
   * Checks if dimensions are valid for printing
   */
  isValidForPrinting(): boolean {
    // Business rule: Common printing constraints
    return this.width >= 100 && this.width <= 2000 &&
           this.height >= 100 && this.height <= 3000;
  }
}

/**
 * Predefined page size dimensions in pixels (at 96 DPI)
 */
const PAGE_SIZE_DIMENSIONS: Record<PageSize, { width: number; height: number }> = {
  A4: { width: 794, height: 1123 }, // 210mm x 297mm
  A5: { width: 559, height: 794 }, // 148mm x 210mm
  A6: { width: 397, height: 559 }, // 105mm x 148mm
  Letter: { width: 816, height: 1056 }, // 8.5" x 11"
  Legal: { width: 816, height: 1344 }, // 8.5" x 14"
  Custom: { width: 800, height: 600 }, // Default custom size
};

/**
 * Gets display name for page size
 */
export function getPageSizeDisplayName(size: PageSize): string {
  const displayNames: Record<PageSize, string> = {
    A4: 'A4 (210 × 297 mm)',
    A5: 'A5 (148 × 210 mm)',
    A6: 'A6 (105 × 148 mm)',
    Letter: 'Letter (8.5 × 11 in)',
    Legal: 'Legal (8.5 × 14 in)',
    Custom: 'Custom'
  };
  return displayNames[size];
}

/**
 * Gets display name for orientation
 */
export function getOrientationDisplayName(orientation: PageOrientation): string {
  return orientation === 'Portrait' ? 'Portrait' : 'Landscape';
}

/**
 * Validates if a page size is valid
 */
export function isValidPageSize(size: string): size is PageSize {
  return ['A4', 'A5', 'A6', 'Letter', 'Legal', 'Custom'].includes(size);
}

/**
 * Validates if an orientation is valid
 */
export function isValidPageOrientation(orientation: string): orientation is PageOrientation {
  return ['Portrait', 'Landscape'].includes(orientation);
}