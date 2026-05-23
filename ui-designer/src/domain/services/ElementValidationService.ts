import { DesignerElement, ElementId } from '../entities/DesignerElement';
import { ElementType } from '../value-objects/ElementType';

/**
 * Domain service for validating designer elements and their relationships.
 * Contains business rules for element validation.
 */
export class ElementValidationService {
  /**
   * Validates if an element can be added to a parent
   */
  canAddChildToParent(parentElement: DesignerElement, childElement: DesignerElement): ValidationResult {
    const errors: string[] = [];

    // Business rule: Parent must be able to contain children
    if (!parentElement.canHaveChildren()) {
      errors.push(`Element type '${parentElement.type}' cannot contain children`);
    }

    // Business rule: Child cannot be added to itself
    if (parentElement.id === childElement.id) {
      errors.push('Element cannot be added to itself');
    }

    // Business rule: Prevent circular references
    if (this.wouldCreateCircularReference(parentElement, childElement.id)) {
      errors.push('Adding this element would create a circular reference');
    }

    // Business rule: Group elements cannot contain other groups
    if (parentElement.isGroup && childElement.isGroup) {
      errors.push('Group elements cannot contain other group elements');
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Validates element properties based on its type
   */
  validateElementProperties(element: DesignerElement): ValidationResult {
    const errors: string[] = [];

    switch (element.type) {
      case 'Text':
        errors.push(...this.validateTextProperties(element.props));
        break;
      case 'Image':
        errors.push(...this.validateImageProperties(element.props));
        break;
      case 'Rectangle':
      case 'Circle':
        errors.push(...this.validateShapeProperties(element.props));
        break;
      case 'Line':
        errors.push(...this.validateLineProperties(element.props));
        break;
      case 'Link':
        errors.push(...this.validateLinkProperties(element.props));
        break;
      case 'List':
        errors.push(...this.validateListProperties(element.props));
        break;
      case 'Table':
        errors.push(...this.validateTableProperties(element.props));
        break;
      case 'Button':
        errors.push(...this.validateButtonProperties(element.props));
        break;
      case 'Checkbox':
      case 'Radio':
        errors.push(...this.validateInputProperties(element.props));
        break;
      // Other element types can be added here
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Validates element positioning and sizing
   */
  validateElementGeometry(element: DesignerElement, pageWidth: number, pageHeight: number): ValidationResult {
    const errors: string[] = [];

    if (element.isPositioned()) {
      // Business rule: Elements should be within page bounds (with some tolerance)
      const tolerance = 50; // Allow elements slightly outside page bounds

      if (element.x! < -tolerance) {
        errors.push('Element is positioned too far left of the page');
      }

      if (element.y! < -tolerance) {
        errors.push('Element is positioned too far above the page');
      }

      if (element.x! > pageWidth + tolerance) {
        errors.push('Element is positioned too far right of the page');
      }

      if (element.y! > pageHeight + tolerance) {
        errors.push('Element is positioned too far below the page');
      }
    }

    if (element.hasDimensions()) {
      // Business rule: Element dimensions should be reasonable
      if (element.width! > pageWidth * 2) {
        errors.push('Element width is too large for the page');
      }

      if (element.height! > pageHeight * 2) {
        errors.push('Element height is too large for the page');
      }
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Validates a collection of elements for consistency
   */
  validateElementCollection(elements: DesignerElement[]): ValidationResult {
    const errors: string[] = [];
    const elementIds = new Set<string>();

    // Check for duplicate IDs
    for (const element of elements) {
      if (elementIds.has(element.id)) {
        errors.push(`Duplicate element ID: ${element.id}`);
      }
      elementIds.add(element.id);
    }

    // Validate parent-child relationships
    for (const element of elements) {
      if (element.children) {
        for (const childId of element.children) {
          if (!elementIds.has(childId)) {
            errors.push(`Element ${element.id} references non-existent child ${childId}`);
          }
        }
      }

      if (element.groupId && !elementIds.has(element.groupId)) {
        errors.push(`Element ${element.id} references non-existent group ${element.groupId}`);
      }
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  private validateTextProperties(props: any): string[] {
    const errors: string[] = [];

    if (!props.text || typeof props.text !== 'string') {
      errors.push('Text elements must have a text property');
    }

    if (props.fontSize !== undefined && (typeof props.fontSize !== 'number' || props.fontSize <= 0)) {
      errors.push('Font size must be a positive number');
    }

    return errors;
  }

  private validateImageProperties(props: any): string[] {
    const errors: string[] = [];

    if (!props.src || typeof props.src !== 'string') {
      errors.push('Image elements must have a src property');
    }

    if (props.width !== undefined && (typeof props.width !== 'number' || props.width <= 0)) {
      errors.push('Image width must be a positive number');
    }

    if (props.height !== undefined && (typeof props.height !== 'number' || props.height <= 0)) {
      errors.push('Image height must be a positive number');
    }

    return errors;
  }

  private validateShapeProperties(props: any): string[] {
    const errors: string[] = [];

    if (props.fillColor && typeof props.fillColor !== 'string') {
      errors.push('Fill color must be a string');
    }

    if (props.strokeColor && typeof props.strokeColor !== 'string') {
      errors.push('Stroke color must be a string');
    }

    if (props.strokeWidth !== undefined && (typeof props.strokeWidth !== 'number' || props.strokeWidth < 0)) {
      errors.push('Stroke width must be a non-negative number');
    }

    return errors;
  }

  private validateLineProperties(props: any): string[] {
    const errors: string[] = [];

    // Line requires coordinates
    const requiredCoords = ['x1', 'y1', 'x2', 'y2'];
    for (const coord of requiredCoords) {
      if (typeof props[coord] !== 'number') {
        errors.push(`Line elements must have a numeric ${coord} property`);
      }
    }

    if (props.strokeColor && typeof props.strokeColor !== 'string') {
      errors.push('Stroke color must be a string');
    }

    if (props.strokeWidth !== undefined && (typeof props.strokeWidth !== 'number' || props.strokeWidth <= 0)) {
      errors.push('Stroke width must be a positive number');
    }

    return errors;
  }

  private validateLinkProperties(props: any): string[] {
    const errors: string[] = [];

    if (!props.url || typeof props.url !== 'string') {
      errors.push('Link elements must have a url property');
    }

    if (props.text && typeof props.text !== 'string') {
      errors.push('Link text must be a string');
    }

    return errors;
  }

  private validateListProperties(props: any): string[] {
    const errors: string[] = [];

    if (props.items && !Array.isArray(props.items)) {
      errors.push('List items must be an array');
    }

    if (props.ordered !== undefined && typeof props.ordered !== 'boolean') {
      errors.push('List ordered property must be a boolean');
    }

    return errors;
  }

  private validateTableProperties(props: any): string[] {
    const errors: string[] = [];

    if (props.rows !== undefined && (typeof props.rows !== 'number' || props.rows <= 0)) {
      errors.push('Table rows must be a positive number');
    }

    if (props.columns !== undefined && (typeof props.columns !== 'number' || props.columns <= 0)) {
      errors.push('Table columns must be a positive number');
    }

    if (props.data && !Array.isArray(props.data)) {
      errors.push('Table data must be an array');
    }

    return errors;
  }

  private validateButtonProperties(props: any): string[] {
    const errors: string[] = [];

    if (!props.text || typeof props.text !== 'string') {
      errors.push('Button elements must have a text property');
    }

    if (props.action && typeof props.action !== 'string') {
      errors.push('Button action must be a string');
    }

    return errors;
  }

  private validateInputProperties(props: any): string[] {
    const errors: string[] = [];

    if (!props.label || typeof props.label !== 'string') {
      errors.push('Input elements must have a label property');
    }

    return errors;
  }

  private wouldCreateCircularReference(parentElement: DesignerElement, childId: ElementId): boolean {
    // This is a simplified check - in a real implementation, you'd need the full element tree
    // For now, we'll assume no circular references exist
    return false;
  }
}

/**
 * Result of a validation operation
 */
export interface ValidationResult {
  isValid: boolean;
  errors: string[];
}