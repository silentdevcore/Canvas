namespace PXA.WebApi.Application.Designer;

public sealed class PxaDesignerTemplateOptions
{
    public const string SectionName = "DesignerTemplates";
    public int MaximumDesignJsonBytes { get; set; } = 10 * 1024 * 1024;
    public int MaximumPageSize { get; set; } = 100;
}
