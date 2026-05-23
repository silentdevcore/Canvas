import { PageSettings } from '../value-objects/PageSettings';

/**
 * Repository interface for page settings.
 * Defines the contract for page configuration data access operations.
 */
export interface IPageRepository {
  /**
   * Saves page settings
   */
  save(settings: PageSettings): Promise<void>;

  /**
   * Gets current page settings
   */
  get(): Promise<PageSettings | null>;

  /**
   * Updates page settings
   */
  update(settings: PageSettings): Promise<void>;

  /**
   * Resets page settings to defaults
   */
  resetToDefaults(): Promise<void>;

  /**
   * Checks if page settings exist
   */
  exists(): Promise<boolean>;
}