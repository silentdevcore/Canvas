import { evaluateExpression, validateExpression, getContextVariables } from '../../src/template/expressionEngine';

describe('Expression Engine', () => {
  describe('evaluateExpression', () => {
    it('should evaluate simple boolean expressions', () => {
      const context = { data: { active: true, count: 5 } };
      expect(evaluateExpression('active', context).value).toBe(true);
      expect(evaluateExpression('count > 3', context).value).toBe(true);
      expect(evaluateExpression('count < 3', context).value).toBe(false);
    });

    it('should evaluate string expressions', () => {
      const context = { data: { name: 'John', status: 'active' } };
      expect(evaluateExpression('name === "John"', context).value).toBe(true);
      expect(evaluateExpression('status !== "inactive"', context).value).toBe(true);
      expect(evaluateExpression('name.includes("oh")', context).value).toBe(true);
    });

    it('should evaluate numeric expressions', () => {
      const context = { data: { price: 100, discount: 0.1, quantity: 2 } };
      expect(evaluateExpression('price * quantity', context).value).toBe(200);
      expect(evaluateExpression('price * (1 - discount)', context).value).toBe(90);
      expect(evaluateExpression('Math.max(price, 50)', context).value).toBe(100);
    });

    it('should evaluate array expressions', () => {
      const context = { data: { items: [1, 2, 3, 4, 5], tags: ['a', 'b', 'c'] } };
      expect(evaluateExpression('items.length > 3', context).value).toBe(true);
      expect(evaluateExpression('items.includes(3)', context).value).toBe(true);
      expect(evaluateExpression('tags.some(tag => tag === "b")', context).value).toBe(true);
    });

    it('should evaluate nested object expressions', () => {
      const context = {
        data: {
          user: { name: 'John', age: 30 },
          order: { total: 100, items: [{ price: 50 }, { price: 50 }] }
        }
      };
      expect(evaluateExpression('user.age >= 18', context).value).toBe(true);
      expect(evaluateExpression('order.total === order.items.length * 50', context).value).toBe(true);
      expect(evaluateExpression('user.name && order.total > 0', context).value).toBe(true);
    });

    it('should handle undefined and null values safely', () => {
      const context = { data: { user: null, optional: undefined, required: 'value' } };
      expect(evaluateExpression('user?.name === undefined', context).value).toBe(true);
      expect(evaluateExpression('optional ?? "default"', context).value).toBe('default');
      expect(evaluateExpression('required || "fallback"', context).value).toBe('value');
    });

    it('should evaluate date expressions', () => {
      const context = { data: { createdAt: '2024-01-01', today: new Date() } };
      expect(evaluateExpression('new Date(createdAt) < new Date()', context).value).toBe(true);
      expect(evaluateExpression('today instanceof Date', context).value).toBe(true);
    });

    it('should prevent dangerous operations', () => {
      const context = { data: { value: 42 } };

      // Should block access to global objects
      expect(evaluateExpression('window', context).isValid).toBe(false);
      expect(evaluateExpression('global', context).isValid).toBe(false);
      expect(evaluateExpression('process', context).isValid).toBe(false);
      expect(evaluateExpression('require', context).isValid).toBe(false);

      // Should block function constructor
      expect(evaluateExpression('new Function("return 1")', context).isValid).toBe(false);
      expect(evaluateExpression('eval("1 + 1")', context).isValid).toBe(false);
    });

    it('should handle malformed expressions gracefully', () => {
      const context = { data: { value: 42 } };

      expect(evaluateExpression('value +', context).isValid).toBe(false);
      expect(evaluateExpression('value.someUndefinedMethod()', context).isValid).toBe(false);
      expect(evaluateExpression('invalid syntax {{{', context).isValid).toBe(false);
    });

    it('should support template literals in expressions', () => {
      const context = { data: { firstName: 'John', lastName: 'Doe' } };
      expect(evaluateExpression('`${firstName} ${lastName}`', context).value).toBe('John Doe');
      expect(evaluateExpression('`Hello, ${firstName}!`', context).value).toBe('Hello, John!');
    });

    it('should evaluate complex business logic expressions', () => {
      const context = {
        data: {
          order: {
            total: 150,
            items: [
              { category: 'electronics', price: 100 },
              { category: 'books', price: 50 }
            ],
            customer: { type: 'premium', discount: 0.1 }
          }
        }
      };

      // Complex expression with multiple conditions
      const expression = `
        order.total > 100 &&
        order.items.some(item => item.category === 'electronics') &&
        (order.customer.type === 'premium' ? order.total * (1 - order.customer.discount) : order.total) < 140
      `;

      expect(evaluateExpression(expression, context).value).toBe(true);
    });
  });

  describe('validateExpression', () => {
    it('should validate safe expressions', () => {
      expect(validateExpression('value > 0').isValid).toBe(true);
      expect(validateExpression('user.name === "John"').isValid).toBe(true);
      expect(validateExpression('items.length > 0').isValid).toBe(true);
    });

    it('should reject dangerous expressions', () => {
      expect(validateExpression('window.alert("hack")').isValid).toBe(false);
      expect(validateExpression('eval("1+1")').isValid).toBe(false);
      expect(validateExpression('new Function("return 1")').isValid).toBe(false);
      expect(validateExpression('process.exit()').isValid).toBe(false);
    });

    it('should reject malformed expressions', () => {
      expect(validateExpression('value +').isValid).toBe(false);
      expect(validateExpression('invalid {{{ syntax').isValid).toBe(false);
      expect(validateExpression('').isValid).toBe(false);
    });
  });

  describe('getContextVariables', () => {
    it('should return available context variables for autocomplete', () => {
      const variables = getContextVariables();

      expect(variables).toBeDefined();
      expect(Array.isArray(variables)).toBe(true);
      expect(variables.length).toBeGreaterThan(0);

      // Check that all variables have required properties
      variables.forEach(variable => {
        expect(variable).toHaveProperty('name');
        expect(variable).toHaveProperty('type');
        expect(variable).toHaveProperty('description');
      });

      // Check for specific variables
      const dataVar = variables.find(v => v.name === '$data');
      expect(dataVar).toBeDefined();
      expect(dataVar!.type).toBe('object');
      expect(dataVar!.description).toContain('Root data object');
    });
  });

  describe('performance and limits', () => {
    it('should handle large data objects efficiently', () => {
      const largeData = {
        items: Array.from({ length: 1000 }, (_, i) => ({ id: i, value: Math.random() })),
        metadata: { total: 1000, processed: true }
      };
      const context = { data: largeData };

      const startTime = Date.now();
      const result = evaluateExpression('items.length === 1000 && metadata.processed', context);
      const endTime = Date.now();

      expect(result.value).toBe(true);
      expect(endTime - startTime).toBeLessThan(100); // Should complete in less than 100ms
    });

    it('should prevent infinite loops and excessive computation', () => {
      const context = { data: { value: 1 } };

      // This should timeout or be prevented
      const result = evaluateExpression('while(true) { value++ }', context);
      expect(result.isValid).toBe(false);
    });
  });
});
