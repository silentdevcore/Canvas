import { ElementId, ElementType, ElementProperties } from '../../domain';

/**
 * Input for adding a new element
 */
export interface AddElementInput {
  type: ElementType;
  props: ElementProperties;
  parentId?: ElementId;
  x?: number;
  y?: number;
}

/**
 * Output from adding a new element
 */
export interface AddElementOutput {
  elementId: ElementId;
  success: boolean;
  error?: string;
}

/**
 * Use case interface for adding elements to the design
 */
export interface IAddElementUseCase {
  /**
   * Execute the add element use case
   */
  execute(input: AddElementInput): Promise<AddElementOutput>;
}