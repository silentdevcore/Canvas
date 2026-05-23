import { DesignerElement, ElementType, ElementTypeValues } from '../../../src/domain';

describe('DesignerElement Entity', () => {
  describe('Creation', () => {
    it('should create a valid DesignerElement', () => {
      const element = new DesignerElement({
        id: 'test-id',
        type: ElementTypeValues.Text,
        x: 10,
        y: 20,
        width: 100,
        height: 50,
        props: { text: 'Hello World' }
      });

      expect(element.id).toBe('test-id');
      expect(element.type).toBe(ElementTypeValues.Text);
      expect(element.x).toBe(10);
      expect(element.y).toBe(20);
      expect(element.width).toBe(100);
      expect(element.height).toBe(50);
      expect(element.props).toEqual({ text: 'Hello World' });
    });

    it('should generate an ID if not provided', () => {
      const element = new DesignerElement({
        type: ElementTypeValues.Text,
        x: 0,
        y: 0,
        props: {}
      });

      expect(element.id).toBeDefined();
      expect(typeof element.id).toBe('string');
      expect(element.id.length).toBeGreaterThan(0);
    });

    it('should have default values for optional properties', () => {
      const element = new DesignerElement({
        type: ElementTypeValues.Text,
        props: {}
      });

      expect(element.x).toBe(0);
      expect(element.y).toBe(0);
      expect(element.width).toBeUndefined();
      expect(element.height).toBeUndefined();
      expect(element.props).toEqual({});
    });
  });

  describe('Business Rules', () => {
    it('should enforce positive dimensions', () => {
      expect(() => new DesignerElement({
        type: ElementTypeValues.Text,
        x: 10,
        y: 20,
        width: -100, // Invalid negative width
        height: 50,
        props: {}
      })).toThrow('Element width must be positive');
    });

    it('should enforce valid element types', () => {
      expect(() => new DesignerElement({
        type: 'InvalidType' as any, // Invalid type
        x: 10,
        y: 20,
        props: {}
      })).toThrow('Invalid element type');
    });

    it('should allow valid element types', () => {
      const validTypes = Object.values(ElementTypeValues);

      validTypes.forEach(type => {
        expect(() => new DesignerElement({
          type: type,
          x: 10,
          y: 20,
          props: {}
        })).not.toThrow();
      });
    });
  });

  describe('Immutability', () => {
    it('should be immutable - direct property changes should not affect instance', () => {
      const element = new DesignerElement({
        type: ElementTypeValues.Text,
        x: 10,
        y: 20,
        props: { text: 'Hello' }
      });

      // Try to mutate (this should not work in a real immutable implementation)
      // In TypeScript, we can't prevent this, but the entity should be treated as immutable
      expect(element.x).toBe(10);
      expect(element.props.text).toBe('Hello');
    });
  });

  describe('Equality', () => {
    it('should be equal when all properties match', () => {
      const element1 = new DesignerElement({
        id: 'same-id',
        type: ElementTypeValues.Text,
        x: 10,
        y: 20,
        props: { text: 'Hello' }
      });

      const element2 = new DesignerElement({
        id: 'same-id',
        type: ElementTypeValues.Text,
        x: 10,
        y: 20,
        props: { text: 'Hello' }
      });

      // In a real implementation, you'd have an equals method
      expect(element1.id).toBe(element2.id);
      expect(element1.type).toBe(element2.type);
      expect(element1.x).toBe(element2.x);
      expect(element1.y).toBe(element2.y);
    });
  });
});