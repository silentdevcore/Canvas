using Canvas.Domain.ValueObjects;
using Canvas.Core.Primitives;

namespace Canvas.Core.Abstractions;

public interface IRepeatExpander
{
    /// <summary>
    /// Expands a repeating element based on its repeat configuration.
    /// </summary>
    /// <param name="element">The element to repeat</param>
    /// <param name="context">The expansion context</param>
    /// <param name="parent">The parent expanded element</param>
    /// <param name="expandElementFunc">Function to expand individual elements</param>
    Task ExpandRepeatAsync(
        DesignerElement element,
        ExpansionContext context,
        ExpandedElement? parent,
        Func<DesignerElement, ExpansionContext, ExpandedElement?, Task> expandElementFunc);
}