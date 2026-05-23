import { ITemplateRepository, DesignTemplate, TemplateMetadata } from '../../domain/repositories/ITemplateRepository';

/**
 * Repository implementation for design templates using localStorage.
 * Implements the ITemplateRepository interface defined in the domain layer.
 * Supports template versioning and metadata persistence.
 */
export class LocalStorageTemplateRepository implements ITemplateRepository {
  private readonly STORAGE_KEY = 'ui-designer-templates';
  private readonly VERSIONS_KEY = 'ui-designer-template-versions';

  async save(template: DesignTemplate): Promise<void> {
    const templates = this.getAllTemplatesFromStorage();
    const existingIndex = templates.findIndex(t => t.id === template.id);

    if (existingIndex >= 0) {
      templates[existingIndex] = { ...template, updatedAt: new Date() };
    } else {
      templates.push(template);
    }

    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(templates));
  }

  async findById(id: string): Promise<DesignTemplate | null> {
    const templates = this.getAllTemplatesFromStorage();
    const template = templates.find(t => t.id === id);

    if (!template) return null;

    // Convert date strings back to Date objects
    return {
      ...template,
      createdAt: new Date(template.createdAt),
      updatedAt: new Date(template.updatedAt)
    };
  }

  async findAll(): Promise<DesignTemplate[]> {
    const templates = this.getAllTemplatesFromStorage();
    return templates.map(template => ({
      ...template,
      createdAt: new Date(template.createdAt),
      updatedAt: new Date(template.updatedAt)
    }));
  }

  async findByName(name: string): Promise<DesignTemplate[]> {
    const templates = this.getAllTemplatesFromStorage();
    const searchName = name.toLowerCase();

    return templates
      .filter(template => template.name.toLowerCase().includes(searchName))
      .map(template => ({
        ...template,
        createdAt: new Date(template.createdAt),
        updatedAt: new Date(template.updatedAt)
      }));
  }

  async findByTags(tags: string[]): Promise<DesignTemplate[]> {
    const templates = this.getAllTemplatesFromStorage();

    return templates
      .filter(template =>
        template.tags && tags.some(tag => template.tags!.includes(tag))
      )
      .map(template => ({
        ...template,
        createdAt: new Date(template.createdAt),
        updatedAt: new Date(template.updatedAt)
      }));
  }

  async deleteById(id: string): Promise<void> {
    const templates = this.getAllTemplatesFromStorage();
    const filteredTemplates = templates.filter(t => t.id !== id);
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(filteredTemplates));
  }

  async exists(id: string): Promise<boolean> {
    const templates = this.getAllTemplatesFromStorage();
    return templates.some(t => t.id === id);
  }

  async count(): Promise<number> {
    const templates = this.getAllTemplatesFromStorage();
    return templates.length;
  }

  async getTemplateNames(): Promise<Array<{ id: string; name: string }>> {
    const templates = this.getAllTemplatesFromStorage();
    return templates.map(template => ({
      id: template.id,
      name: template.name
    }));
  }

  async createVersion(templateId: string, versionName?: string): Promise<DesignTemplate> {
    const template = await this.findById(templateId);
    if (!template) {
      throw new Error(`Template with id ${templateId} not found`);
    }

    // Create a new version with incremented version number
    const currentVersion = template.metadata?.version || '1.0.0';
    const versionParts = currentVersion.split('.');
    const newVersion = `${versionParts[0]}.${versionParts[1]}.${parseInt(versionParts[2]) + 1}`;

    const versionedTemplate: DesignTemplate = {
      ...template,
      id: `${templateId}_v${Date.now()}`, // Unique version ID
      metadata: {
        ...template.metadata,
        version: versionName || newVersion,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        createdBy: template.metadata?.updatedBy || 'User',
        updatedBy: template.metadata?.updatedBy || 'User'
      }
    };

    // Save version to versions storage
    const versions = this.getAllVersionsFromStorage();
    versions.push(versionedTemplate);
    localStorage.setItem(this.VERSIONS_KEY, JSON.stringify(versions));

    return versionedTemplate;
  }

  async getVersions(templateId: string): Promise<DesignTemplate[]> {
    const versions = this.getAllVersionsFromStorage();
    return versions
      .filter(v => v.id.startsWith(`${templateId}_v`))
      .map(version => ({
        ...version,
        createdAt: new Date(version.createdAt),
        updatedAt: new Date(version.updatedAt)
      }))
      .sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
  }

  async getLatestVersion(templateId: string): Promise<DesignTemplate | null> {
    const versions = await this.getVersions(templateId);
    return versions.length > 0 ? versions[0] : null;
  }

  async restoreVersion(templateId: string, versionId: string): Promise<DesignTemplate> {
    const versions = this.getAllVersionsFromStorage();
    const version = versions.find(v => v.id === versionId);

    if (!version) {
      throw new Error(`Version ${versionId} not found`);
    }

    // Create a new template based on the version
    const restoredTemplate: DesignTemplate = {
      ...version,
      id: templateId, // Restore to original ID
      updatedAt: new Date(),
      metadata: {
        ...version.metadata,
        updatedAt: new Date().toISOString(),
        updatedBy: version.metadata?.updatedBy || 'User'
      }
    };

    // Save as the current template
    await this.save(restoredTemplate);

    return restoredTemplate;
  }

  private getAllTemplatesFromStorage(): DesignTemplate[] {
    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (!stored) return [];

      const templates = JSON.parse(stored) as DesignTemplate[];
      return templates;
    } catch (error) {
      console.error('Error reading templates from localStorage:', error);
      return [];
    }
  }

  private getAllVersionsFromStorage(): DesignTemplate[] {
    try {
      const stored = localStorage.getItem(this.VERSIONS_KEY);
      if (!stored) return [];

      const versions = JSON.parse(stored) as DesignTemplate[];
      return versions;
    } catch (error) {
      console.error('Error reading template versions from localStorage:', error);
      return [];
    }
  }
}