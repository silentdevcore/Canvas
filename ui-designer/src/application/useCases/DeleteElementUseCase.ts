import { IDeleteElementUseCase, DeleteElementInput, DeleteElementOutput } from './IDeleteElementUseCase';
import { IElementRepository } from '../../domain';

/**
 * Use case for deleting elements from the design.
 * Handles element removal and cascading deletion of children.
 */
export class DeleteElementUseCase implements IDeleteElementUseCase {
  constructor(
    private readonly elementRepository: IElementRepository
  ) {}

  async execute(input: DeleteElementInput): Promise<DeleteElementOutput> {
    try {
      // Find the element to delete
      const element = await this.elementRepository.findById(input.elementId);
      if (!element) {
        return {
          success: false,
          error: `Element with ID ${input.elementId} not found`
        };
      }

      // Check if element is locked
      if (element.isLocked()) {
        return {
          success: false,
          error: 'Cannot delete a locked element'
        };
      }

      // Get all child elements that need to be deleted
      const childElements = element.children || [];
      const allElementsToDelete = [input.elementId, ...childElements];

      // Delete all elements (parent and children)
      await this.elementRepository.deleteAll(allElementsToDelete);

      return {
        success: true,
        deletedChildrenCount: childElements.length
      };

    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error occurred'
      };
    }
  }
}