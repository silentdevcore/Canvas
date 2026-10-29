namespace PVAD.Export.Tests;

// Full-corpus rendering and external office conversion must not compete for resources.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DocumentFidelityCollection
{
    public const string Name = "Document fidelity integration";
}
