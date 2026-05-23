import { IElementRepository } from '../../domain/repositories/IElementRepository';
import { DesignerElement, ElementId } from '../../domain';
import { ZustandStoreAdapter } from '../adapters/ZustandStoreAdapter';

/**
 * Repository implementation using Zustand store as the data source.
 * Implements the IElementRepository interface defined in the domain layer.
 */
export class ZustandElementRepository implements IElementRepository {
  async save(element: DesignerElement): Promise<void> {
    await ZustandStoreAdapter.saveElement(element);
  }

  async saveAll(elements: DesignerElement[]): Promise<void> {
    await ZustandStoreAdapter.saveAllElements(elements);
  }

  async findById(id: ElementId): Promise<DesignerElement | null> {
    return await ZustandStoreAdapter.findElementById(id);
  }

  async findAll(): Promise<DesignerElement[]> {
    return await ZustandStoreAdapter.findAllElements();
  }

  async findByParentId(parentId: ElementId): Promise<DesignerElement[]> {
    return await ZustandStoreAdapter.findElementsByParentId(parentId);
  }

  async findRootElements(): Promise<DesignerElement[]> {
    return await ZustandStoreAdapter.findRootElements();
  }

  async findByType(type: string): Promise<DesignerElement[]> {
    return await ZustandStoreAdapter.findElementsByType(type);
  }

  async deleteById(id: ElementId): Promise<void> {
    await ZustandStoreAdapter.deleteElementById(id);
  }

  async deleteAll(ids: ElementId[]): Promise<void> {
    await ZustandStoreAdapter.deleteAllElements(ids);
  }

  async exists(id: ElementId): Promise<boolean> {
    return await ZustandStoreAdapter.elementExists(id);
  }

  async count(): Promise<number> {
    return await ZustandStoreAdapter.countElements();
  }

  async clear(): Promise<void> {
    await ZustandStoreAdapter.clearAllElements();
  }
}