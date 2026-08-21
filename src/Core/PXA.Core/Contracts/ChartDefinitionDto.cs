namespace PXA.Core.Contracts;

public static class PxaChartTypes
{
    public const string Bar = "bar";
    public const string Line = "line";
    public const string Area = "area";
    public const string Pie = "pie";
    public const string Doughnut = "doughnut";
    public const string StackedBar = "stackedBar";
    public const string Combo = "combo";

    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Bar, Line, Area, Pie, Doughnut, StackedBar, Combo
    };
}

public sealed class ChartDefinitionDto
{
    public int SchemaVersion { get; set; } = 2;
    public string Type { get; set; } = PxaChartTypes.Bar;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Locale { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<ChartSeriesDto> Series { get; set; } = [];
    public ChartAxisDto? CategoryAxis { get; set; }
    public List<ChartAxisDto> ValueAxes { get; set; } = [];
    public ChartLegendDto? Legend { get; set; }
    public ChartDataLabelsDto? DataLabels { get; set; }
    public List<string> Palette { get; set; } = [];
    public ChartBindingDto? Binding { get; set; }
    public ChartRecognitionDto? Recognition { get; set; }
}

public sealed class ChartSeriesDto
{
    public string Id { get; set; } = "series-1";
    public string Name { get; set; } = "Series 1";
    public string? Type { get; set; }
    public List<double?> Values { get; set; } = [];
    public string? Color { get; set; }
    public string? StackGroup { get; set; }
    public string? ValueAxisId { get; set; }
    public bool Fill { get; set; }
    public bool ShowMarkers { get; set; } = true;
}

public sealed class ChartAxisDto
{
    public string Id { get; set; } = "primary";
    public string? Title { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Scale { get; set; } = "linear";
    public string? NumberFormat { get; set; }
    public bool Visible { get; set; } = true;
    public bool GridLines { get; set; } = true;
}

public sealed class ChartLegendDto
{
    public bool Visible { get; set; } = true;
    public string Position { get; set; } = "bottom";
}

public sealed class ChartDataLabelsDto
{
    public bool Visible { get; set; }
    public string Position { get; set; } = "auto";
    public string? NumberFormat { get; set; }
}

public sealed class ChartBindingDto
{
    public string? DataPath { get; set; }
    public string? CategoryField { get; set; }
    public string? SeriesField { get; set; }
    public string? ValueField { get; set; }
    public string Aggregation { get; set; } = "sum";
    public string Sort { get; set; } = "source";
}

public sealed class ChartRecognitionDto
{
    public string Status { get; set; } = "native";
    public double Confidence { get; set; } = 1;
    public string? SourceKind { get; set; }
    public string? SourceAssetId { get; set; }
    public string? DiagnosticCode { get; set; }
}
