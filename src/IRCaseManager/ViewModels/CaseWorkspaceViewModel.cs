using IRCaseManager.Models;

namespace IRCaseManager.ViewModels;

public class CaseWorkspaceViewModel
{
    public required Case Case { get; init; }

    public required IReadOnlyList<PlaybookStepViewModel> PlaybookSteps { get; init; }

    public EvidenceMetadataViewModel NewEvidence { get; init; } = new();

    public bool CanWorkCase { get; init; }
}

public class PlaybookStepViewModel
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public bool IsCompleted { get; init; }
}
