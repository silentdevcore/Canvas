using Canvas.Core.Abstractions;
using Canvas.Domain.ValueObjects;

namespace Canvas.Core.Primitives;

public sealed class RepeatExpander : IRepeatExpander
{
    public async Task ExpandRepeatAsync(
        DesignerElement element,
        ExpansionContext context,
        ExpandedElement? parent,
        Func<DesignerElement, ExpansionContext, ExpandedElement?, Task> expandElementFunc)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);

        if (element.Repeat == null || string.IsNullOrEmpty(element.Repeat.RepeatSource))
        {
            return;
        }

        // Get the data source for repetition
        var dataSource = ResolveDataSource(element.Repeat.RepeatSource, context.Payload);
        if (dataSource == null)
        {
            // No data to repeat, handle empty state
            await HandleEmptyStateAsync(element, context, parent, expandElementFunc);
            return;
        }

        // Convert to enumerable
        var items = ConvertToEnumerable(dataSource);
        if (!items.Any())
        {
            // Empty collection, handle empty state
            await HandleEmptyStateAsync(element, context, parent, expandElementFunc);
            return;
        }

        // Apply limits
        var maxItems = element.Repeat.MaxItems ?? int.MaxValue;
        var limitedItems = items.Take(maxItems);

        // Expand each item
        foreach (var (item, index) in limitedItems.Select((item, index) => (item, index)))
        {
            await ExpandRepeatItemAsync(element, context, parent, expandElementFunc, item, index);
        }

        // Handle page breaks between items if specified
        if (element.Repeat.PageBreakBetweenItems == true)
        {
            // Add page break logic would go here
            // This would typically involve adding page break elements or metadata
        }
    }

    private object? ResolveDataSource(string dataPath, Dictionary<string, object> payload)
    {
        var pathParts = dataPath.Split('.');
        object? current = payload;

        foreach (var part in pathParts)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private IEnumerable<object> ConvertToEnumerable(object dataSource)
    {
        if (dataSource is IEnumerable<object> enumerable)
        {
            return enumerable;
        }

        if (dataSource is System.Collections.IEnumerable oldEnumerable)
        {
            var result = new List<object>();
            foreach (var item in oldEnumerable)
            {
                result.Add(item);
            }
            return result;
        }

        // Single item, wrap in collection
        return new[] { dataSource };
    }

    private async Task HandleEmptyStateAsync(
        DesignerElement element,
        ExpansionContext context,
        ExpandedElement? parent,
        Func<DesignerElement, ExpansionContext, ExpandedElement?, Task> expandElementFunc)
    {
        if (element.Repeat?.EmptyBehavior == "hide-table")
        {
            // Don't add anything
            return;
        }

        if (element.Repeat?.EmptyBehavior == "show-placeholder-text")
        {
            // Create a placeholder element with empty text
            var placeholderElement = CreatePlaceholderElement(element, element.Repeat.EmptyRowText ?? "No data available");
            await expandElementFunc(placeholderElement, context, parent);
        }
        else
        {
            // Default behavior: keep template (show empty version)
            await expandElementFunc(element, context, parent);
        }
    }

    private async Task ExpandRepeatItemAsync(
        DesignerElement element,
        ExpansionContext context,
        ExpandedElement? parent,
        Func<DesignerElement, ExpansionContext, ExpandedElement?, Task> expandElementFunc,
        object item,
        int index)
    {
        // Create a modified context with loop variables
        var loopContext = new Dictionary<string, object>(context.Payload);

        // Add loop variables
        if (!string.IsNullOrEmpty(element.Repeat.ItemAlias))
        {
            loopContext[element.Repeat.ItemAlias] = item;
        }

        if (!string.IsNullOrEmpty(element.Repeat.IndexAlias))
        {
            loopContext[element.Repeat.IndexAlias] = index;
        }

        // Convert item to dictionary if it's a complex object
        if (item is Dictionary<string, object> itemDict)
        {
            // Merge item properties into context
            foreach (var kvp in itemDict)
            {
                loopContext[kvp.Key] = kvp.Value;
            }
        }

        var itemContext = new ExpansionContext
        {
            Template = context.Template,
            Payload = loopContext,
            ExpandedElements = context.ExpandedElements,
            ElementIndex = context.ElementIndex
        };

        // Expand the element with the loop context
        await expandElementFunc(element, itemContext, parent);

        // Update the global element index
        context.ElementIndex = itemContext.ElementIndex;
    }

    private DesignerElement CreatePlaceholderElement(DesignerElement originalElement, string placeholderText)
    {
        // Create a copy of the element with placeholder content
        var placeholder = new DesignerElement
        {
            Id = $"{originalElement.Id}_placeholder_{Guid.NewGuid():N}",
            Type = originalElement.Type,
            Props = new Dictionary<string, object>(originalElement.Props),
            Binding = originalElement.Binding,
            Expression = originalElement.Expression,
            Repeat = null, // Don't repeat the placeholder
            Overflow = originalElement.Overflow,
            Image = originalElement.Image,
            Table = originalElement.Table,
            Validation = originalElement.Validation,
            Children = originalElement.Children,
            X = originalElement.X,
            Y = originalElement.Y,
            Width = originalElement.Width,
            Height = originalElement.Height,
            IsGroup = originalElement.IsGroup,
            GroupId = originalElement.GroupId,
            Locked = originalElement.Locked
        };

        // Set placeholder text for text elements
        if (originalElement.Type == ElementType.Text && placeholder.Props.ContainsKey("text"))
        {
            placeholder.Props["text"] = placeholderText;
        }

        return placeholder;
    }
}