import { IAddElementUseCase, AddElementInput, AddElementOutput } from './IAddElementUseCase';
import {
  IElementRepository,
  DesignerElement,
  DesignerElementFactory,
  ElementValidationService,
  ElementId
} from '../../domain';

/**
 * Use case for adding elements to the design.
 * Orchestrates element creation, validation, and persistence.
 */
export class AddElementUseCase implements IAddElementUseCase {
  constructor(
    private readonly elementRepository: IElementRepository,
    private readonly validationService: ElementValidationService
  ) {}

  async execute(input: AddElementInput): Promise<AddElementOutput> {
    try {
      // Generate unique ID for the new element
      const elementId: ElementId = this.generateElementId();

      // Create the element using the factory
      const element = this.createElementFromInput(elementId, input);

      // Validate the element
      const validationResult = this.validationService.validateElementProperties(element);
      if (!validationResult.isValid) {
        return {
          elementId,
          success: false,
          error: `Validation failed: ${validationResult.errors.join(', ')}`
        };
      }

      // If parent is specified, validate parent-child relationship
      if (input.parentId) {
        const parentElement = await this.elementRepository.findById(input.parentId);
        if (!parentElement) {
          return {
            elementId,
            success: false,
            error: `Parent element with ID ${input.parentId} not found`
          };
        }

        const parentChildValidation = this.validationService.canAddChildToParent(parentElement, element);
        if (!parentChildValidation.isValid) {
          return {
            elementId,
            success: false,
            error: `Cannot add child to parent: ${parentChildValidation.errors.join(', ')}`
          };
        }
      }

      // Save the element
      await this.elementRepository.save(element);

      return {
        elementId,
        success: true
      };

    } catch (error) {
      return {
        elementId: '' as ElementId, // This shouldn't happen in success case
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred'
      };
    }
  }

  private generateElementId(): ElementId {
    return `element_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  private createElementFromInput(elementId: ElementId, input: AddElementInput): DesignerElement {
    // Use the factory to create elements with proper defaults
    let element: DesignerElement;
    switch (input.type) {
      case 'Text':
        element = DesignerElementFactory.createTextElement(
          elementId,
          input.props.text || 'New Text',
          input.props.fontSize || 16
        );
        break;

      case 'Image':
        element = DesignerElementFactory.createImageElement(
          elementId,
          input.props.src || '',
          input.props.width || 200,
          input.props.height || 100
        );
        break;

      case 'Rectangle':
        element = DesignerElementFactory.createRectangleElement(
          elementId,
          input.props.width || 200,
          input.props.height || 100
        );
        break;

      case 'Column':
        element = DesignerElementFactory.createColumnElement(
          elementId,
          input.props.children || []
        );
        break;

      default:
        // For other element types, create with provided props
        element = new DesignerElement({
          id: elementId,
          type: input.type,
          props: input.props,
          children: input.props.children,
          x: input.x,
          y: input.y,
          width: input.props.width,
          height: input.props.height
        });
        break;
    }

    // Preserve explicit geometry from input regardless of element factory defaults.
    const geometryInput = input as AddElementInput & { width?: number; height?: number };
    const width = geometryInput.width ?? input.props.width;
    const height = geometryInput.height ?? input.props.height;

    return new DesignerElement({
      id: element.id,
      type: element.type,
      props: element.props,
      children: element.children,
      x: input.x ?? element.x,
      y: input.y ?? element.y,
      width: width ?? element.width,
      height: height ?? element.height,
      isGroup: element.isGroup,
      groupId: element.groupId,
      locked: element.locked,
    });
  }
}