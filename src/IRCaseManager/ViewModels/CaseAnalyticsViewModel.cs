namespace IRCaseManager.ViewModels;

public class CaseAnalyticsViewModel
{
    public string SelectedRange { get; init; } = "7d";

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

    public IReadOnlyList<AnalyticsBreakdownItem> SeverityBreakdown { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownItem> StatusBreakdown { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownItem> CaseTypeBreakdown { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownItem> QueueBreakdown { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownItem> LevelBreakdown { get; init; } = [];

    public IReadOnlyList<AnalyticsBreakdownItem> CreatedOverTime { get; init; } = [];
}

public class AnalyticsBreakdownItem
{
    public required string Label { get; init; }

    public int Count { get; init; }

    public int Percent { get; init; }
}
