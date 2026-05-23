import { PageSize, PageOrientation } from './PageSize';

/**
 * Domain value object representing page settings with business rules and validation.
 * Immutable value object that encapsulates page configuration.
 */
export class PageSettings {
  constructor(
    public readonly size: PageSize,
    public readonly orientation: PageOrientation,
    public readonly width: number,
    public readonly height: number,
    public readonly backgroundColor: string,
    public readonly margins: PageMargins,
    public readonly title: string,
    public readonly description: string
  ) {
    this.validate();
  }

  /**
   * Business rule: Page dimensions must be positive
   */
  private validate(): void {
    if (this.width <= 0 || this.height <= 0) {
      throw new Error('Page dimensions must be positive');
    }

    if (this.margins.top < 0 || this.margins.right < 0 ||
        this.margins.bottom < 0 || this.margins.left < 0) {
      throw new Error('Page margins cannot be negative');
    }

    // Business rule: Margins cannot exceed page dimensions
    if (this.margins.left + this.margins.right >= this.width ||
        this.margins.top + this.margins.bottom >= this.height) {
      throw new Error('Margins cannot exceed page dimensions');
    }
  }

  /**
   * Creates a new PageSettings instance with updated properties
   */
  update(updates: Partial<PageSettingsUpdate>): PageSettings {
    return new PageSettings(
      updates.size ?? this.size,
      updates.orientation ?? this.orientation,
      updates.width ?? this.width,
      updates.height ?? this.height,
      updates.backgroundColor ?? this.backgroundColor,
      updates.margins ?? this.margins,
      updates.title ?? this.title,
      updates.description ?? this.description
    );
  }

  /**
   * Gets the printable area dimensions (page size minus margins)
   */
  getPrintableArea(): { width: number; height: number } {
    return {
      width: this.width - this.margins.left - this.margins.right,
      height: this.height - this.margins.top - this.margins.bottom
    };
  }

  /**
   * Checks if a point is within the printable area
   */
  isWithinPrintableArea(x: number, y: number): boolean {
    const area = this.getPrintableArea();
    return x >= this.margins.left &&
           x <= this.margins.left + area.width &&
           y >= this.margins.top &&
           y <= this.margins.top + area.height;
  }
}

/**
 * Value object for page margins
 */
export class PageMargins {
  constructor(
    public readonly top: number,
    public readonly right: number,
    public readonly bottom: number,
    public readonly left: number
  ) {
    if (top < 0 || right < 0 || bottom < 0 || left < 0) {
      throw new Error('Margins cannot be negative');
    }
  }

  /**
   * Gets the total horizontal margin
   */
  get totalHorizontal(): number {
    return this.left + this.right;
  }

  /**
   * Gets the total vertical margin
   */
  get totalVertical(): number {
    return this.top + this.bottom;
  }
}

/**
 * Type for updating page settings
 */
export interface PageSettingsUpdate {
  size?: PageSize;
  orientation?: PageOrientation;
  width?: number;
  height?: number;
  backgroundColor?: string;
  margins?: PageMargins;
  title?: string;
  description?: string;
}