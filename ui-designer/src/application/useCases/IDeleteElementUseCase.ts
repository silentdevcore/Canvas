import { ElementId } from '../../domain';

/**
 * Input for deleting an element
 */
export interface DeleteElementInput {
  elementId: ElementId;
}

/**
 * Output from deleting an element
 */
export interface DeleteElementOutput {
  success: boolean;
  error?: string;
  deletedChildrenCount?: number;
}

/**
 * Use case interface for deleting elements from the design
 */
export interface IDeleteElementUseCase {
  /**
   * Execute the delete element use case
   */
  execute(input: DeleteElementInput): Promise<DeleteElementOutput>;
}