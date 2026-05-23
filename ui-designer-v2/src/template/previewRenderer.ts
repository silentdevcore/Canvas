import { Template, SimpleElement } from '../store';
import { evaluateExpression, ExpressionContext } from './expressionEngine';
import { applyFormatter, FormatterConfig } from './formatters';
import { expandRepeat, RepeatConfig } from './repeatExpander';

export function renderPreview(template: Template, data: Record<string, any>): SimpleElement[] {
  const renderedElements: SimpleElement[] = [];

  (template.pages?.flatMap((p: any) => p.elements) ?? []).forEach((element: SimpleElement) => {
    const context: ExpressionContext = { data, element };

    // Apply repeat if configured
    if (element.repeat) {
      const repeatResult = expandRepeat(element.repeat as RepeatConfig, context);
      repeatResult.instances.forEach((instance) => {
        const repeatedElement = { ...element, ...instance.context.element }; // Merge context
        const rendered = applyBindingAndExpression(repeatedElement, instance.context);
        if (rendered) renderedElements.push(rendered);
      });
    } else {
      const rendered = applyBindingAndExpression(element, context);
      if (rendered) renderedElements.push(rendered);
    }
  });

  return renderedElements;
}

function applyBindingAndExpression(element: SimpleElement, context: ExpressionContext): SimpleElement | null {
  // Check visibility expression
  if (element.expression) {
    const visibleResult = evaluateExpression(element.expression, context);
    if (visibleResult.isValid && !visibleResult.value) return null;
  }

  // Apply binding
  if (element.binding) {
    const boundValue = evaluateExpression(element.binding, context).value;
    if (element.type === 'chart') {
      element.chartData = boundValue;
    } else if (['dropdown', 'optionlist', 'radio'].includes(element.type)) {
      element.options = boundValue;
    } else {
      element.content = boundValue !== undefined ? String(boundValue) : element.content;
    }
  }

  // Apply formatter
  if (element.formatter && element.content) {
    const formatterConfig: FormatterConfig = { type: element.formatter as any }; // Simplify for now
    const formatted = applyFormatter(element.content, formatterConfig);
    if (formatted.isValid) element.content = formatted.value;
  }

  return element;
}
