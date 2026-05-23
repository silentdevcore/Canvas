import { IPageRepository } from '../../domain/repositories/IPageRepository';
import { PageSettings } from '../../domain';
import { ZustandStoreAdapter } from '../adapters/ZustandStoreAdapter';

/**
 * Repository implementation for page settings using Zustand store.
 * Implements the IPageRepository interface defined in the domain layer.
 */
export class ZustandPageRepository implements IPageRepository {
  async save(settings: PageSettings): Promise<void> {
    await ZustandStoreAdapter.savePageSettings(settings);
  }

  async get(): Promise<PageSettings | null> {
    return await ZustandStoreAdapter.getPageSettings();
  }

  async update(settings: PageSettings): Promise<void> {
    await ZustandStoreAdapter.updatePageSettings(settings);
  }

  async resetToDefaults(): Promise<void> {
    await ZustandStoreAdapter.resetPageSettingsToDefaults();
  }

  async exists(): Promise<boolean> {
    return await ZustandStoreAdapter.pageSettingsExist();
  }
}