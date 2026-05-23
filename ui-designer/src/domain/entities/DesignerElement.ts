import { ElementType, isValidElementType } from '../value-objects/ElementType';

/**
 * Unique identifier for designer elements
 */
export type ElementId = string;

/**
 * Domain entity representing a UI element in the designer.
 * Contains business rules and validation for element properties.
 */
export interface DesignerElementProps {
  id?: ElementId;
  type: ElementType;
  props: ElementProperties;
  children?: ElementId[];
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  isGroup?: boolean;
  groupId?: ElementId;
  locked?: boolean;
}

export class DesignerElement {
  public readonly id: ElementId;
  public readonly type: ElementType;
  public readonly props: ElementProperties;
  public readonly children?: ElementId[];
  public readonly x?: number;
  public readonly y?: number;
  public readonly width?: number;
  public readonly height?: number;
  public readonly isGroup?: boolean;
  public readonly groupId?: ElementId;
  public readonly locked?: boolean;

  constructor(props: DesignerElementProps) {
    this.id = props.id || this.generateId();
    this.type = props.type;
    this.props = props.props;
    this.children = props.children;
    this.x = props.x ?? 0;
    this.y = props.y ?? 0;
    this.width = props.width;
    this.height = props.height;
    this.isGroup = props.isGroup ?? false;
    this.groupId = props.groupId;
    this.locked = props.locked ?? false;

    this.validate();
  }

  private generateId(): ElementId {
    return `element_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Business rules validation
   */
  private validate(): void {
    if (!this.id || this.id.trim() === '') {
      throw new Error('Element ID cannot be empty');
    }

    if (!isValidElementType(this.type)) {
      throw new Error(`Invalid element type: ${this.type}`);
    }

    // Business rule: Group elements must have children
    if (this.isGroup && (!this.children || this.children.length === 0)) {
      throw new Error('Group elements must have children');
    }

    // Business rule: Child elements cannot be groups themselves
    if (this.children && this.children.length > 0 && this.isGroup === false) {
      // This is allowed - non-group elements can have children (like Column)
    }

    // Business rule: Position and dimensions must be non-negative when specified
    if (this.x !== undefined && this.x < 0) {
      throw new Error('Element x position cannot be negative');
    }
    if (this.y !== undefined && this.y < 0) {
      throw new Error('Element y position cannot be negative');
    }
    if (this.width !== undefined && this.width <= 0) {
      throw new Error('Element width must be positive');
    }
    if (this.height !== undefined && this.height <= 0) {
      throw new Error('Element height must be positive');
    }
  }

  /**
   * Creates a new element with updated properties
   */
  updateProps(newProps: Partial<ElementProperties>): DesignerElement {
    return new DesignerElement({
      id: this.id,
      type: this.type,
      props: { ...this.props, ...newProps },
      children: this.children,
      x: this.x,
      y: this.y,
      width: this.width,
      height: this.height,
      isGroup: this.isGroup,
      groupId: this.groupId,
      locked: this.locked
    });
  }

  /**
   * Creates a new element with updated position
   */
  updatePosition(x: number, y: number): DesignerElement {
    return new DesignerElement({
      id: this.id,
      type: this.type,
      props: this.props,
      children: this.children,
      x,
      y,
      width: this.width,
      height: this.height,
      isGroup: this.isGroup,
      groupId: this.groupId,
      locked: this.locked
    });
  }

  /**
   * Creates a new element with updated size
   */
  updateSize(width: number, height: number): DesignerElement {
    return new DesignerElement({
      id: this.id,
      type: this.type,
      props: this.props,
      children: this.children,
      x: this.x,
      y: this.y,
      width,
      height,
      isGroup: this.isGroup,
      groupId: this.groupId,
      locked: this.locked
    });
  }

  /**
   * Checks if this element can contain children
   */
  canHaveChildren(): boolean {
    // Business rule: Certain element types can contain children
    return ['Column', 'Grid', 'Table'].includes(this.type);
  }

  /**
   * Checks if this element is positioned (has x,y coordinates)
   */
  isPositioned(): boolean {
    return this.x !== undefined && this.y !== undefined;
  }

  /**
   * Checks if this element has defined dimensions
   */
  hasDimensions(): boolean {
    return this.width !== undefined && this.height !== undefined;
  }

  /**
   * Gets the bounding box of this element
   */
  getBoundingBox(): { x: number; y: number; width: number; height: number } | null {
    if (!this.isPositioned() || !this.hasDimensions()) {
      return null;
    }

    return {
      x: this.x!,
      y: this.y!,
      width: this.width!,
      height: this.height!
    };
  }

  /**
   * Checks if a point is within this element's bounds
   */
  containsPoint(x: number, y: number): boolean {
    const bounds = this.getBoundingBox();
    if (!bounds) return false;

    return x >= bounds.x &&
           x <= bounds.x + bounds.width &&
           y >= bounds.y &&
           y <= bounds.y + bounds.height;
  }

  /**
   * Checks if this element is locked
   */
  isLocked(): boolean {
    return this.locked === true;
  }

  /**
   * Checks if this element is part of a group
   */
  isInGroup(): boolean {
    return this.groupId !== undefined;
  }
}

/**
 * Type for element properties - flexible to accommodate different element types
 */
export type ElementProperties = Record<string, any>;

/**
 * Factory function to create elements with proper validation
 */
export class DesignerElementFactory {
  static createTextElement(id: ElementId, text: string = 'Text', fontSize: number = 16): DesignerElement {
    return new DesignerElement({
      id,
      type: 'Text',
      props: { text, fontSize }
    });
  }

  static createImageElement(id: ElementId, src: string, width: number = 200, height: number = 100): DesignerElement {
    return new DesignerElement({
      id,
      type: 'Image',
      props: { src, width, height, alt: 'Image' }
    });
  }

  static createRectangleElement(id: ElementId, width: number = 200, height: number = 100): DesignerElement {
    return new DesignerElement({
      id,
      type: 'Rectangle',
      props: {
        width,
        height,
        fillColor: '#ffffff',
        strokeColor: '#000000',
        strokeWidth: 1,
        borderRadius: 0
      }
    });
  }

  static createColumnElement(id: ElementId, children: ElementId[] = []): DesignerElement {
    return new DesignerElement({
      id,
      type: 'Column',
      props: {},
      children
    });
  }
}
