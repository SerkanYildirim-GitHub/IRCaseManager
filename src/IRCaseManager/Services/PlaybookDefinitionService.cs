using IRCaseManager.Models;

namespace IRCaseManager.Services;

public record PlaybookStepDefinition(string Key, string Title, string Description);

public class PlaybookDefinitionService
{
    public IReadOnlyList<PlaybookStepDefinition> GetSteps(CaseType caseType)
    {
        return caseType switch
        {
            CaseType.Phishing => Phishing,
            CaseType.Malware => Malware,
            CaseType.UnauthorizedAccess => UnauthorizedAccess,
            _ => Generic
        };
    }

    private static readonly PlaybookStepDefinition[] Phishing =
    [
        new("phishing-triage", "Validate report", "Confirm the message, sender, recipients, and source reference before taking response action."),
        new("phishing-headers", "Review headers and links", "Document relevant header details, URLs, attachment names, and visible impersonation indicators."),
        new("phishing-scope", "Identify impacted users", "Determine who received or interacted with the message and whether credentials or endpoints may be affected."),
        new("phishing-containment", "Coordinate containment", "Track mailbox, URL, attachment, and credential containment decisions in the case notes."),
        new("phishing-lessons", "Prepare closure notes", "Summarize findings, impact, actions taken, and prevention opportunities.")
    ];

    private static readonly PlaybookStepDefinition[] Malware =
    [
        new("malware-triage", "Confirm detection", "Document alert source, host, user, file path, hash, and first observed time."),
        new("malware-scope", "Scope affected assets", "Identify related hosts, users, processes, persistence points, and lateral movement indicators."),
        new("malware-evidence", "Record evidence metadata", "Capture metadata for logs, hashes, memory, disk, or EDR artifacts without uploading files in this slice."),
        new("malware-containment", "Track containment", "Document isolation, blocking, eradication, and recovery decisions."),
        new("malware-review", "Review residual risk", "Confirm whether additional monitoring, reimaging, or credential actions are required.")
    ];

    private static readonly PlaybookStepDefinition[] UnauthorizedAccess =
    [
        new("access-validate", "Validate access concern", "Confirm account, asset, source IP, authentication pattern, and triggering detection."),
        new("access-scope", "Scope identity and asset activity", "Review affected users, sessions, privilege changes, and related systems."),
        new("access-containment", "Track containment actions", "Document session revocation, password reset, MFA actions, and access restrictions."),
        new("access-timeline", "Build activity timeline", "Record key authentication and post-authentication events in chronological order."),
        new("access-closure", "Document closure rationale", "Summarize confirmed impact, response actions, and follow-up monitoring.")
    ];

    private static readonly PlaybookStepDefinition[] Generic =
    [
        new("generic-triage", "Triage case", "Confirm the alert, source reference, severity, initial scope, and assigned response owner."),
        new("generic-evidence", "Document evidence metadata", "Record relevant sources, timestamps, references, and analyst notes."),
        new("generic-timeline", "Build timeline", "Capture important investigation and response events in order."),
        new("generic-findings", "Document findings", "Record observables, IOCs, affected assets, and analyst conclusions."),
        new("generic-closure", "Prepare closure report", "Summarize impact, actions taken, open risk, and handoff items.")
    ];
}
