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
        const repeatedElement = applyRepeatInstance(element, instance.item, instance.index);
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

function applyRepeatInstance(element: SimpleElement, item: any, index: number): SimpleElement {
  const repeatedElement: SimpleElement = {
    ...element,
    id: `${element.id}__repeat_${index}`,
    name: element.name ? `${element.name} ${index + 1}` : element.name,
    y: element.y + element.height * index,
    repeat: undefined
  };
  const values = item && typeof item === 'object' && !Array.isArray(item)
    ? { ...item, index }
    : { value: item, index };

  repeatedElement.content = substituteTokens(repeatedElement.content, values);
  repeatedElement.htmlContent = substituteTokens(repeatedElement.htmlContent, values);
  if ((repeatedElement as any).cellData) {
    (repeatedElement as any).cellData = (repeatedElement as any).cellData.map((row: string[]) =>
      row.map((cell) => substituteTokens(cell, values) ?? '')
    );
  }
  return repeatedElement;
}

function substituteTokens(value: string | undefined, values: Record<string, any>): string | undefined {
  if (!value) return value;
  return value.replace(/\{\{\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\}\}/g, (match, key) => {
    const resolved = key.split('.').reduce((current: any, part: string) => current?.[part], values);
    return resolved == null ? match : String(resolved);
  });
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
