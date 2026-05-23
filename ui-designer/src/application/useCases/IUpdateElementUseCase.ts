import { ElementId, ElementProperties } from '../../domain';

/**
 * Input for updating an element
 */
export interface UpdateElementInput {
  elementId: ElementId;
  props?: Partial<ElementProperties>;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
}

/**
 * Output from updating an element
 */
export interface UpdateElementOutput {
  success: boolean;
  error?: string;
}

/**
 * Use case interface for updating elements in the design
 */
export interface IUpdateElementUseCase {
  /**
   * Execute the update element use case
   */
  execute(input: UpdateElementInput): Promise<UpdateElementOutput>;
}