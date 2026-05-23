import { DesignerElement } from '../entities/DesignerElement';
import { PageSettings } from '../value-objects/PageSettings';

/**
 * Template metadata structure
 */
export interface TemplateMetadata {
  id?: string;
  name?: string;
  description?: string;
  category?: string;
  tags?: string[];
  version?: string;
  schemaVersion?: string;
  createdBy?: string;
  updatedBy?: string;
  createdAt?: string;
  updatedAt?: string;
  locale?: string;
  currency?: string;
  timezone?: string;
  formattingProfile?: {
    dateFormat?: string;
    timeFormat?: string;
    numberFormat?: string;
    currencyFormat?: string;
  };
  migrationHints?: Record<string, any>;
  isPublic?: boolean;
  isArchived?: boolean;
}

/**
 * Template data structure
 */
export interface DesignTemplate {
  id: string;
  name: string;
  description?: string;
  elements: DesignerElement[];
  pageSettings: PageSettings;
  createdAt: Date;
  updatedAt: Date;
  tags?: string[];
  metadata?: TemplateMetadata;
}

/**
 * Repository interface for design templates.
 * Defines the contract for template data access operations.
 */
export interface ITemplateRepository {
  /**
   * Saves a template
   */
  save(template: DesignTemplate): Promise<void>;

  /**
   * Finds a template by ID
   */
  findById(id: string): Promise<DesignTemplate | null>;

  /**
   * Finds all templates
   */
  findAll(): Promise<DesignTemplate[]>;

  /**
   * Finds templates by name (partial match)
   */
  findByName(name: string): Promise<DesignTemplate[]>;

  /**
   * Finds templates by tags
   */
  findByTags(tags: string[]): Promise<DesignTemplate[]>;

  /**
   * Deletes a template by ID
   */
  deleteById(id: string): Promise<void>;

  /**
   * Checks if a template exists
   */
  exists(id: string): Promise<boolean>;

  /**
   * Gets the total count of templates
   */
  count(): Promise<number>;

  /**
   * Gets template names for quick access
   */
  getTemplateNames(): Promise<Array<{ id: string; name: string }>>;

  /**
   * Creates a new version of a template
   */
  createVersion(templateId: string, versionName?: string): Promise<DesignTemplate>;

  /**
   * Gets all versions of a template
   */
  getVersions(templateId: string): Promise<DesignTemplate[]>;

  /**
   * Gets the latest version of a template
   */
  getLatestVersion(templateId: string): Promise<DesignTemplate | null>;

  /**
   * Restores a template to a specific version
   */
  restoreVersion(templateId: string, versionId: string): Promise<DesignTemplate>;
}
