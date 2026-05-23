import { LocalStorageTemplateRepository } from '../../../src/infrastructure/repositories/LocalStorageTemplateRepository';
import { DesignTemplate, TemplateMetadata } from '../../../src/domain/repositories/ITemplateRepository';

// Mock localStorage
const localStorageMock = {
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
  clear: jest.fn(),
};
Object.defineProperty(window, 'localStorage', { value: localStorageMock });

describe('LocalStorageTemplateRepository', () => {
  let repository: LocalStorageTemplateRepository;
  let mockTemplate: DesignTemplate;
  let mockMetadata: TemplateMetadata;

  beforeEach(() => {
    repository = new LocalStorageTemplateRepository();
    jest.clearAllMocks();

    mockMetadata = {
      id: 'template-1',
      name: 'Test Template',
      description: 'A test template',
      category: 'invoice',
      tags: ['test', 'invoice'],
      version: '1.0.0',
      schemaVersion: '1.0',
      createdBy: 'test-user',
      updatedBy: 'test-user',
      createdAt: '2024-01-01T00:00:00.000Z',
      updatedAt: '2024-01-01T00:00:00.000Z',
      locale: 'en-US',
      currency: 'USD',
      timezone: 'America/New_York',
      formattingProfile: {
        dateFormat: 'MM/dd/yyyy',
        timeFormat: 'HH:mm:ss',
        numberFormat: 'en-US',
        currencyFormat: 'USD'
      },
      isPublic: false,
      isArchived: false
    };

    mockTemplate = {
      id: 'template-1',
      name: 'Test Template',
      description: 'A test template',
      elements: [
        {
          id: 'element-1',
          type: 'text' as any,
          props: { text: 'Hello World' },
          x: 10,
          y: 10,
          width: 100,
          height: 20
        }
      ] as any,
      pageSettings: {
        width: 595,
        height: 842,
        orientation: 'portrait',
        margins: { top: 20, right: 20, bottom: 20, left: 20 }
      } as any,
      createdAt: new Date('2024-01-01T00:00:00.000Z'),
      updatedAt: new Date('2024-01-01T00:00:00.000Z'),
      tags: ['test'],
      metadata: mockMetadata
    };
  });

  describe('save', () => {
    it('should save a template with metadata', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      await repository.save(mockTemplate);

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'ui-designer-templates',
        JSON.stringify([mockTemplate])
      );
    });

    it('should update existing template', async () => {
      const existingTemplate = { ...mockTemplate, name: 'Old Name' };
      localStorageMock.getItem.mockReturnValue(JSON.stringify([existingTemplate]));

      const updatedTemplate = { ...mockTemplate, name: 'Updated Name' };
      await repository.save(updatedTemplate);

      // The repository updates the updatedAt field, so we need to account for that
      const expectedTemplates = [{ ...updatedTemplate, updatedAt: expect.any(Date) }];
      const callArgs = localStorageMock.setItem.mock.calls[0];
      expect(callArgs[0]).toBe('ui-designer-templates');
      const savedTemplates = JSON.parse(callArgs[1]);
      expect(savedTemplates[0].name).toBe('Updated Name');
      expect(savedTemplates[0].id).toBe('template-1');
    });
  });

  describe('findById', () => {
    it('should return template with converted dates', async () => {
      localStorageMock.getItem.mockReturnValue(JSON.stringify([mockTemplate]));

      const result = await repository.findById('template-1');

      expect(result).toBeDefined();
      expect(result!.id).toBe('template-1');
      expect(result!.createdAt).toBeInstanceOf(Date);
      expect(result!.updatedAt).toBeInstanceOf(Date);
      expect(result!.metadata).toEqual(mockMetadata);
    });

    it('should return null for non-existent template', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      const result = await repository.findById('non-existent');

      expect(result).toBeNull();
    });
  });

  describe('findAll', () => {
    it('should return all templates with converted dates', async () => {
      const templates = [mockTemplate];
      localStorageMock.getItem.mockReturnValue(JSON.stringify(templates));

      const result = await repository.findAll();

      expect(result).toHaveLength(1);
      expect(result[0].createdAt).toBeInstanceOf(Date);
      expect(result[0].updatedAt).toBeInstanceOf(Date);
    });
  });

  describe('findByName', () => {
    it('should find templates by partial name match', async () => {
      const templates = [
        mockTemplate,
        { ...mockTemplate, id: 'template-2', name: 'Another Template' }
      ];
      localStorageMock.getItem.mockReturnValue(JSON.stringify(templates));

      const result = await repository.findByName('Test');

      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Test Template');
    });
  });

  describe('findByTags', () => {
    it('should find templates by tags', async () => {
      const templates = [
        mockTemplate,
        { ...mockTemplate, id: 'template-2', tags: ['other'] }
      ];
      localStorageMock.getItem.mockReturnValue(JSON.stringify(templates));

      const result = await repository.findByTags(['test']);

      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('template-1');
    });
  });

  describe('deleteById', () => {
    it('should delete template by id', async () => {
      const templates = [mockTemplate];
      localStorageMock.getItem.mockReturnValue(JSON.stringify(templates));

      await repository.deleteById('template-1');

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'ui-designer-templates',
        JSON.stringify([])
      );
    });
  });

  describe('exists', () => {
    it('should return true for existing template', async () => {
      localStorageMock.getItem.mockReturnValue(JSON.stringify([mockTemplate]));

      const result = await repository.exists('template-1');

      expect(result).toBe(true);
    });

    it('should return false for non-existent template', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      const result = await repository.exists('non-existent');

      expect(result).toBe(false);
    });
  });

  describe('count', () => {
    it('should return template count', async () => {
      const templates = [mockTemplate, mockTemplate];
      localStorageMock.getItem.mockReturnValue(JSON.stringify(templates));

      const result = await repository.count();

      expect(result).toBe(2);
    });
  });

  describe('getTemplateNames', () => {
    it('should return template names', async () => {
      localStorageMock.getItem.mockReturnValue(JSON.stringify([mockTemplate]));

      const result = await repository.getTemplateNames();

      expect(result).toEqual([{ id: 'template-1', name: 'Test Template' }]);
    });
  });

  describe('createVersion', () => {
    it('should create a new version of template', async () => {
      localStorageMock.getItem
        .mockReturnValueOnce(JSON.stringify([mockTemplate])) // for findById
        .mockReturnValueOnce('[]'); // for getAllVersionsFromStorage

      const result = await repository.createVersion('template-1', '1.1.0');

      expect(result.id).toMatch(/^template-1_v\d+$/);
      expect(result.metadata?.version).toBe('1.1.0');
      expect(result.metadata?.createdBy).toBe('test-user');
      expect(result.metadata?.updatedBy).toBe('test-user');
    });

    it('should auto-increment version if not provided', async () => {
      localStorageMock.getItem
        .mockReturnValueOnce(JSON.stringify([mockTemplate])) // for findById
        .mockReturnValueOnce('[]'); // for getAllVersionsFromStorage

      const result = await repository.createVersion('template-1');

      expect(result.metadata?.version).toBe('1.0.1');
    });

    it('should throw error for non-existent template', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      await expect(repository.createVersion('non-existent')).rejects.toThrow(
        'Template with id non-existent not found'
      );
    });
  });

  describe('getVersions', () => {
    it('should return all versions of a template', async () => {
      const version1 = { ...mockTemplate, id: 'template-1_v1234567890', createdAt: new Date('2024-01-01T00:00:00.000Z') };
      const version2 = { ...mockTemplate, id: 'template-1_v1234567891', createdAt: new Date('2024-01-02T00:00:00.000Z') };
      localStorageMock.getItem.mockReturnValue(JSON.stringify([version1, version2]));

      const result = await repository.getVersions('template-1');

      expect(result).toHaveLength(2);
      expect(result[0].createdAt).toBeInstanceOf(Date);
      expect(result[1].createdAt).toBeInstanceOf(Date);
      // Should be sorted by createdAt descending (newest first)
      expect(result[0].id).toBe('template-1_v1234567891');
      expect(result[1].id).toBe('template-1_v1234567890');
    });
  });

  describe('getLatestVersion', () => {
    it('should return the latest version', async () => {
      const version1 = { ...mockTemplate, id: 'template-1_v1234567890', createdAt: new Date('2024-01-01T00:00:00.000Z') };
      const version2 = { ...mockTemplate, id: 'template-1_v1234567891', createdAt: new Date('2024-01-02T00:00:00.000Z') };
      localStorageMock.getItem.mockReturnValue(JSON.stringify([version1, version2]));

      const result = await repository.getLatestVersion('template-1');

      expect(result).toBeDefined();
      expect(result!.id).toBe('template-1_v1234567891');
    });

    it('should return null if no versions exist', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      const result = await repository.getLatestVersion('template-1');

      expect(result).toBeNull();
    });
  });

  describe('restoreVersion', () => {
    it('should restore a template to a specific version', async () => {
      const version = {
        ...mockTemplate,
        id: 'template-1_v1234567890',
        name: 'Versioned Template'
      };
      localStorageMock.getItem
        .mockReturnValueOnce(JSON.stringify([version])) // for getAllVersionsFromStorage
        .mockReturnValueOnce('[]'); // for getAllTemplatesFromStorage in save

      const result = await repository.restoreVersion('template-1', 'template-1_v1234567890');

      expect(result.id).toBe('template-1');
      expect(result.name).toBe('Versioned Template');
      expect(result.metadata?.updatedAt).toBe(new Date().toISOString());
    });

    it('should throw error for non-existent version', async () => {
      localStorageMock.getItem.mockReturnValue('[]');

      await expect(repository.restoreVersion('template-1', 'non-existent-version')).rejects.toThrow(
        'Version non-existent-version not found'
      );
    });
  });
});