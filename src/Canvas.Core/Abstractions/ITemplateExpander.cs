using Canvas.Domain.Entities;
using Canvas.Domain.ValueObjects;

namespace Canvas.Core.Abstractions;

public interface ITemplateExpander
{
    /// <summary>
    /// Expands a template with the provided data payload to create a renderable document model.
    /// </summary>
    /// <param name="template">The template to expand</param>
    /// <param name="payload">The data payload to merge into the template</param>
    /// <returns>A document model ready for rendering</returns>
    Task<object> ExpandAsync(DesignTemplate template, object payload);
}