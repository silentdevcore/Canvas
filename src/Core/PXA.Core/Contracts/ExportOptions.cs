namespace PXA.Core.Contracts;

public record ExportOptions(
	float? Dpi = null,
	int? Quality = null,
	CancellationToken CancellationToken = default,
	bool WordFidelityV2 = true);
