import { DesignerElement, ElementId } from '../entities/DesignerElement';

/**
 * Repository interface for designer elements.
 * Defines the contract for element data access operations.
 */
export interface IElementRepository {
  /**
   * Saves an element
   */
  save(element: DesignerElement): Promise<void>;

  /**
   * Saves multiple elements
   */
  saveAll(elements: DesignerElement[]): Promise<void>;

  /**
   * Finds an element by ID
   */
  findById(id: ElementId): Promise<DesignerElement | null>;

  /**
   * Finds all elements
   */
  findAll(): Promise<DesignerElement[]>;

  /**
   * Finds elements by parent ID
   */
  findByParentId(parentId: ElementId): Promise<DesignerElement[]>;

  /**
   * Finds root elements (elements without parents)
   */
  findRootElements(): Promise<DesignerElement[]>;

  /**
   * Finds elements by type
   */
  findByType(type: string): Promise<DesignerElement[]>;

  /**
   * Deletes an element by ID
   */
  deleteById(id: ElementId): Promise<void>;

  /**
   * Deletes multiple elements
   */
  deleteAll(ids: ElementId[]): Promise<void>;

  /**
   * Checks if an element exists
   */
  exists(id: ElementId): Promise<boolean>;

  /**
   * Gets the total count of elements
   */
  count(): Promise<number>;

  /**
   * Clears all elements
   */
  clear(): Promise<void>;
}