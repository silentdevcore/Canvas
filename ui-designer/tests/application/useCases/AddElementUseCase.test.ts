import { AddElementUseCase } from '../../../src/application';
import { ElementTypeValues } from '../../../src/domain';

// Mock the dependencies
const mockElementRepository = {
  save: jest.fn(),
  findById: jest.fn(),
  findAll: jest.fn(),
  delete: jest.fn(),
  update: jest.fn(),
  exists: jest.fn()
};

const mockValidationService = {
  validateElementProperties: jest.fn(),
  validateElementUpdate: jest.fn()
};

describe('AddElementUseCase', () => {
  let useCase: AddElementUseCase;

  beforeEach(() => {
    // Reset mocks
    jest.clearAllMocks();

    // Create use case instance
    useCase = new AddElementUseCase(
      mockElementRepository as any,
      mockValidationService as any
    );
  });

  describe('execute', () => {
    it('should successfully add a valid element', async () => {
      // Arrange
      const elementData = {
        type: ElementTypeValues.Text,
        props: { text: 'Hello World' },
        x: 10,
        y: 20
      };

      const savedElement = {
        id: 'mock-generated-id',
        ...elementData,
        width: undefined,
        height: undefined
      };

      mockValidationService.validateElementProperties.mockReturnValue({ isValid: true });
      mockElementRepository.save.mockResolvedValue(savedElement);

      // Act
      const result = await useCase.execute(elementData);

      // Assert
      expect(result.success).toBe(true);
      expect(result.elementId).toBeDefined();
      expect(typeof result.elementId).toBe('string');
      expect(result.elementId).toMatch(/^element_\d+_[a-z0-9]+$/); // Dynamic ID pattern
      expect(result.error).toBeUndefined();

      expect(mockValidationService.validateElementProperties).toHaveBeenCalled();
      expect(mockElementRepository.save).toHaveBeenCalledWith(
        expect.objectContaining({
          type: ElementTypeValues.Text,
          props: expect.objectContaining({ text: 'Hello World' }),
          x: 10,
          y: 20
        })
      );
    });

    it('should return validation error for invalid element', async () => {
      // Arrange
      const invalidElementData = {
        type: 'InvalidType' as any,
        props: {},
        x: 10,
        y: 20
      };

      const validationError = { isValid: false, errors: ['Invalid element type'] };
      mockValidationService.validateElementProperties.mockReturnValue(validationError);

      // Act
      const result = await useCase.execute(invalidElementData);

      // Assert
      expect(result.success).toBe(false);
      expect(result.elementId).toBeDefined();
      expect(result.error).toContain('Invalid element type');

      expect(mockValidationService.validateElementProperties).not.toHaveBeenCalled();
      expect(mockElementRepository.save).not.toHaveBeenCalled();
    });

    it('should handle repository errors', async () => {
      // Arrange
      const elementData = {
        type: ElementTypeValues.Text,
        props: { text: 'Hello World' },
        x: 10,
        y: 20
      };

      const repositoryError = new Error('Database connection failed');
      mockValidationService.validateElementProperties.mockReturnValue({ isValid: true });
      mockElementRepository.save.mockRejectedValue(repositoryError);

      // Act
      const result = await useCase.execute(elementData);

      // Assert
      expect(result.success).toBe(false);
      expect(result.error).toBe('Database connection failed');

      expect(mockValidationService.validateElementProperties).toHaveBeenCalled();
      expect(mockElementRepository.save).toHaveBeenCalled();
    });

    it('should add element with default position when not specified', async () => {
      // Arrange
      const elementData = {
        type: ElementTypeValues.Text,
        props: { text: 'Hello World' }
        // x and y not specified
      };

      const savedElement = {
        id: 'default-position-id',
        ...elementData,
        x: 0,
        y: 0,
        width: undefined,
        height: undefined
      };

      mockValidationService.validateElementProperties.mockReturnValue({ isValid: true });
      mockElementRepository.save.mockResolvedValue(savedElement);

      // Act
      const result = await useCase.execute(elementData);

      // Assert
      expect(result.success).toBe(true);
      expect(result.elementId).toBeDefined();
      expect(typeof result.elementId).toBe('string');

      expect(mockElementRepository.save).toHaveBeenCalledWith(
        expect.objectContaining({
          type: ElementTypeValues.Text,
          props: expect.objectContaining({ text: 'Hello World' }),
          x: 0,
          y: 0
        })
      );
    });

    it('should preserve width and height when specified', async () => {
      // Arrange
      const elementData = {
        type: ElementTypeValues.Text,
        props: { text: 'Hello World' },
        x: 10,
        y: 20,
        width: 200,
        height: 100
      };

      const savedElement = {
        id: 'dimensions-id',
        ...elementData
      };

      mockValidationService.validateElementProperties.mockReturnValue({ isValid: true });
      mockElementRepository.save.mockResolvedValue(savedElement);

      // Act
      const result = await useCase.execute(elementData);

      // Assert
      expect(result.success).toBe(true);
      expect(result.elementId).toBeDefined();
      expect(typeof result.elementId).toBe('string');

      expect(mockElementRepository.save).toHaveBeenCalledWith(
        expect.objectContaining({
          type: ElementTypeValues.Text,
          props: expect.objectContaining({ text: 'Hello World' }),
          x: 10,
          y: 20,
          width: 200,
          height: 100
        })
      );
    });
  });
});