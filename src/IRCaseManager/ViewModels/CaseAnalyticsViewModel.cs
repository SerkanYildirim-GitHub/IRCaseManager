namespace IRCaseManager.ViewModels;

public class CaseAnalyticsViewModel
{
    public string SelectedRange { get; init; } = "7d";

    public string ChartMode { get; init; } = "donut";

    public string RangeLabel { get; init; } = "Last 7 Days";

    public string? FromInput { get; init; }

    public string? ToInput { get; init; }

    public string? ValidationMessage { get; init; }

    public int TotalCases { get; init; }

    public int OpenCases { get; init; }

    public int ClosedCases { get; init; }

    public int EscalatedCases { get; init; }

    public int CriticalCases { get; init; }

    public int HighCases { get; init; }

    public bool HasCases => TotalCases > 0;

    public AnalyticsBreakdownSection SeverityBreakdown { get; init; } = AnalyticsBreakdownSection.Empty(
        "Case Severity Distribution",
        "Cases grouped by severity in the selected range.");

    public AnalyticsBreakdownSection StatusBreakdown { get; init; } = AnalyticsBreakdownSection.Empty(
        "Case Lifecycle Status",
        "Cases grouped by current workflow status.");

    public AnalyticsBreakdownSection CaseTypeBreakdown { get; init; } = AnalyticsBreakdownSection.Empty(
        "Incident Type Distribution",
        "Cases grouped by incident category.");

    public AnalyticsBreakdownSection QueueBreakdown { get; init; } = AnalyticsBreakdownSection.Empty(
        "Workload by Queue",
        "Cases grouped by current handling queue.");

    public AnalyticsBreakdownSection LevelBreakdown { get; init; } = AnalyticsBreakdownSection.Empty(
        "Workload by Response Level",
        "Cases grouped by assigned response tier.");

    public IReadOnlyList<AnalyticsBreakdownItem> CreatedOverTime { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownSection> BreakdownSections =>
    [
        SeverityBreakdown,
        StatusBreakdown,
        CaseTypeBreakdown,
        QueueBreakdown,
        LevelBreakdown
    ];
}

public class AnalyticsBreakdownSection
{
    public required string Title { get; init; }

    public string Description { get; init; } = string.Empty;

    public int Total { get; init; }

    public string DonutGradient { get; init; } = string.Empty;

    public bool HasData => Total > 0;

    public IReadOnlyList<AnalyticsBreakdownItem> Items { get; init; } = [];

    public static AnalyticsBreakdownSection Empty(string title, string description)
    {
        return new AnalyticsBreakdownSection
        {
            Title = title,
            Description = description
        };
    }
}

public class AnalyticsBreakdownItem
{
    public required string Label { get; init; }

    public int Count { get; init; }

    public int Percent { get; init; }

    public int BarPercent { get; init; }

    public string SegmentStart { get; init; } = "0";

    public string SegmentLength { get; init; } = "0";

    public string Color { get; init; } = "#64748b";

    public string ColorClass { get; init; } = "analytics-color-neutral";
}
