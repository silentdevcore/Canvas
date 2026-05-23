using Canvas.Core.Abstractions;
using Canvas.Domain.Entities;
using Canvas.Domain.ValueObjects;
using System.Text.Json;

namespace Canvas.Core.Primitives;

public sealed class TemplateExpander : ITemplateExpander
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly IValueFormatter _valueFormatter;
    private readonly IRepeatExpander _repeatExpander;

    public TemplateExpander(
        IExpressionEvaluator expressionEvaluator,
        IValueFormatter valueFormatter,
        IRepeatExpander repeatExpander)
    {
        _expressionEvaluator = expressionEvaluator;
        _valueFormatter = valueFormatter;
        _repeatExpander = repeatExpander;
    }

    public async Task<object> ExpandAsync(DesignTemplate template, object payload)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(payload);

        // Convert payload to dictionary for easier access
        var payloadDict = ConvertToDictionary(payload);

        // Create expansion context
        var context = new ExpansionContext
        {
            Template = template,
            Payload = payloadDict,
            ExpandedElements = new List<ExpandedElement>(),
            ElementIndex = 0
        };

        // Process all root elements
        foreach (var rootElement in template.Elements.Where(e => IsRootElement(e, template.Elements)))
        {
            await ExpandElementAsync(rootElement, context, null);
        }

        // Convert to document model format
        return ConvertToDocumentModel(context);
    }

    private async Task ExpandElementAsync(DesignerElement element, ExpansionContext context, ExpandedElement? parent)
    {
        // Evaluate visibility condition
        if (!await EvaluateVisibilityAsync(element, context))
        {
            return; // Skip invisible elements
        }

        // Handle repeat expansion
        if (element.Repeat != null && !string.IsNullOrEmpty(element.Repeat.RepeatSource))
        {
            await _repeatExpander.ExpandRepeatAsync(element, context, parent, ExpandElementAsync);
            return;
        }

        // Create expanded element
        var expandedElement = await CreateExpandedElementAsync(element, context);

        // Add to parent's children or root list
        if (parent != null)
        {
            parent.Children.Add(expandedElement);
        }
        else
        {
            context.ExpandedElements.Add(expandedElement);
        }

        // Process children
        if (element.Children != null)
        {
            foreach (var childId in element.Children)
            {
                var childElement = context.Template.Elements.FirstOrDefault(e => e.Id == childId);
                if (childElement != null)
                {
                    await ExpandElementAsync(childElement, context, expandedElement);
                }
            }
        }
    }

    private async Task<bool> EvaluateVisibilityAsync(DesignerElement element, ExpansionContext context)
    {
        if (string.IsNullOrEmpty(element.Expression?.VisibleWhen))
        {
            return true; // Visible by default
        }

        var result = await _expressionEvaluator.EvaluateAsync(element.Expression.VisibleWhen, context.Payload);
        return result.IsValid && Convert.ToBoolean(result.Value);
    }

    private async Task<ExpandedElement> CreateExpandedElementAsync(DesignerElement element, ExpansionContext context)
    {
        // Resolve properties with bindings and expressions
        var resolvedProps = await ResolvePropertiesAsync(element, context);

        var expandedElement = new ExpandedElement
        {
            Id = element.Id,
            Type = element.Type,
            Props = resolvedProps,
            Children = new List<ExpandedElement>(),
            Index = context.ElementIndex++
        };

        // Copy layout properties
        expandedElement.X = element.X;
        expandedElement.Y = element.Y;
        expandedElement.Width = element.Width;
        expandedElement.Height = element.Height;

        return expandedElement;
    }

    private async Task<Dictionary<string, object>> ResolvePropertiesAsync(DesignerElement element, ExpansionContext context)
    {
        var resolvedProps = new Dictionary<string, object>();

        foreach (var prop in element.Props)
        {
            object resolvedValue = prop.Value;

            // Check for binding
            if (element.Binding?.DataPath != null && prop.Key == GetBindableProperty(element.Type))
            {
                resolvedValue = ResolveBinding(element.Binding, context.Payload);
            }

            // Check for expression
            if (element.Expression?.ValueExpression != null && prop.Key == GetBindableProperty(element.Type))
            {
                var exprResult = await _expressionEvaluator.EvaluateAsync(element.Expression.ValueExpression, context.Payload);
                if (exprResult.IsValid)
                {
                    resolvedValue = exprResult.Value;
                }
            }

            // Apply formatting if specified
            if (element.Binding?.Formatter != null)
            {
                resolvedValue = _valueFormatter.Format(resolvedValue, element.Binding.Formatter);
            }

            resolvedProps[prop.Key] = resolvedValue;
        }

        return resolvedProps;
    }

    private object ResolveBinding(BindingConfig binding, Dictionary<string, object> payload)
    {
        if (string.IsNullOrEmpty(binding.DataPath))
        {
            return binding.FallbackValue ?? "";
        }

        var pathParts = binding.DataPath.Split('.');
        object current = payload;

        foreach (var part in pathParts)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                return binding.FallbackValue ?? "";
            }
        }

        return current ?? binding.FallbackValue ?? "";
    }

    private bool IsRootElement(DesignerElement element, IEnumerable<DesignerElement> allElements)
    {
        return !allElements.Any(e => e.Children?.Contains(element.Id) == true);
    }

    private string GetBindableProperty(ElementType type)
    {
        return type switch
        {
            ElementType.Text => "text",
            ElementType.Image => "src",
            ElementType.QRCode => "value",
            ElementType.Barcode => "value",
            ElementType.Signature => "imagePath",
            ElementType.RichText => "html",
            ElementType.Link => "url",
            ElementType.Button => "text",
            ElementType.Checkbox => "label",
            ElementType.Radio => "label",
            _ => "value"
        };
    }

    private Dictionary<string, object> ConvertToDictionary(object obj)
    {
        if (obj is Dictionary<string, object> dict)
        {
            return dict;
        }

        // Try to deserialize JSON string
        if (obj is string jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString)
                       ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        // Convert using reflection (simplified)
        var result = new Dictionary<string, object>();
        var properties = obj.GetType().GetProperties();
        foreach (var prop in properties)
        {
            result[prop.Name] = prop.GetValue(obj) ?? "";
        }
        return result;
    }

    private object ConvertToDocumentModel(ExpansionContext context)
    {
        // Convert expanded elements to the document model format expected by PDF renderer
        return new
        {
            PageSettings = context.Template.PageSettings,
            Elements = context.ExpandedElements.Select(e => new
            {
                e.Id,
                e.Type,
                e.Props,
                e.X,
                e.Y,
                e.Width,
                e.Height,
                Children = e.Children.Select(c => c.Id).ToList()
            }).ToList()
        };
    }
}

public class ExpansionContext
{
    public required DesignTemplate Template { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
    public required List<ExpandedElement> ExpandedElements { get; init; }
    public int ElementIndex { get; set; }
}

public class ExpandedElement
{
    public required string Id { get; init; }
    public required ElementType Type { get; init; }
    public required Dictionary<string, object> Props { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public required List<ExpandedElement> Children { get; init; }
    public int Index { get; set; }
}