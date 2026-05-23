import { IUpdateElementUseCase, UpdateElementInput, UpdateElementOutput } from './IUpdateElementUseCase';
import {
  IElementRepository,
  ElementValidationService
} from '../../domain';

/**
 * Use case for updating elements in the design.
 * Handles property updates, position changes, and size modifications.
 */
export class UpdateElementUseCase implements IUpdateElementUseCase {
  constructor(
    private readonly elementRepository: IElementRepository,
    private readonly validationService: ElementValidationService
  ) {}

  async execute(input: UpdateElementInput): Promise<UpdateElementOutput> {
    try {
      // Find the existing element
      const existingElement = await this.elementRepository.findById(input.elementId);
      if (!existingElement) {
        return {
          success: false,
          error: `Element with ID ${input.elementId} not found`
        };
      }

      // Check if element is locked
      if (existingElement.isLocked()) {
        return {
          success: false,
          error: 'Cannot update a locked element'
        };
      }

      // Create updated element
      let updatedElement = existingElement;

      // Update properties if provided
      if (input.props) {
        updatedElement = updatedElement.updateProps(input.props);
      }

      // Update position if provided
      if (input.x !== undefined && input.y !== undefined) {
        updatedElement = updatedElement.updatePosition(input.x, input.y);
      }

      // Update size if provided
      if (input.width !== undefined && input.height !== undefined) {
        updatedElement = updatedElement.updateSize(input.width, input.height);
      }

      // Validate the updated element
      const validationResult = this.validationService.validateElementProperties(updatedElement);
      if (!validationResult.isValid) {
        return {
          success: false,
          error: `Validation failed: ${validationResult.errors.join(', ')}`
        };
      }

      // Save the updated element
      await this.elementRepository.save(updatedElement);

      return {
        success: true
      };

    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred'
      };
    }
  }
}