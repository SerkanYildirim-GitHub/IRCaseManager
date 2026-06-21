using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class CaseWorkspaceViewModel
{
    public required Case Case { get; init; }

    public required IReadOnlyList<PlaybookStepViewModel> PlaybookSteps { get; init; }

    public EvidenceMetadataViewModel NewEvidence { get; init; } = new();

    public InvestigationDetailsViewModel InvestigationDetails { get; init; } = new();

    public TimelineEntryViewModel NewTimelineEntry { get; init; } = new();

    public bool CanWorkCase { get; init; }

    public bool CanCloseCase { get; init; }

    public bool CanReopenCase { get; init; }

    public bool CanEscalateCase { get; init; }
}

public class PlaybookStepViewModel
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public bool IsManuallyCompleted { get; init; }

    public bool IsAutoCompleted { get; init; }

    public bool IsComplete => IsManuallyCompleted || IsAutoCompleted;

    public bool IsCompleted => IsComplete;

    public string? AutoCompletionReason { get; init; }
}
