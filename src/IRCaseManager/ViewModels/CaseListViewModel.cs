using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class CaseListViewModel
{
    public IReadOnlyList<Case> Cases { get; init; } = [];

    public CaseQueueSummaryViewModel Summary { get; init; } = new();

    public string PageTitle { get; init; } = "All Cases";

    public string Subtitle { get; init; } = "All cases you are authorized to view.";

    public CaseSeverity? SelectedSeverity { get; init; }

    public CaseStatus? SelectedStatus { get; init; }
}

public class CaseQueueSummaryViewModel
{
    public int TotalCases { get; init; }

    public int CriticalCases { get; init; }

    public int HighCases { get; init; }

    public int MediumCases { get; init; }

    public int LowCases { get; init; }

    public int InformationalCases { get; init; }

    public int NewCases { get; init; }

    public int AssignedCases { get; init; }

    public int EscalatedCases { get; init; }

    public int ClosedCases { get; init; }
}
