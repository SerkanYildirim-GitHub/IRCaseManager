using IRCaseManager.Models;

namespace IRCaseManager.Services;

[Flags]
public enum PlaybookAutoCompletionSignals
{
    None = 0,
    DetectionSource = 1,
    AlertReportedAt = 2,
    AffectedUsers = 4,
    AffectedAssets = 8,
    InvolvedAppsOrTools = 16,
    InitialFindings = 32,
    Evidence = 64,
    IocSummary = 128,
    ContainmentActions = 256,
    EscalationReason = 512,
    ClosureSummary = 1024
}

public record PlaybookStepDefinition(
    string Key,
    string Title,
    string Description,
    PlaybookAutoCompletionSignals AutoCompletionSignals = PlaybookAutoCompletionSignals.None);

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

    private static readonly PlaybookStepDefinition[] CommonFoundationalSteps =
    [
        new("record-detection-source", "Record detection source", "Document where the case was detected or reported, such as EDR, SIEM, user report, or threat intelligence.", PlaybookAutoCompletionSignals.DetectionSource),
        new("record-alert-report-time", "Record alert/report time", "Document when the alert, report, or suspected activity was first observed or reported.", PlaybookAutoCompletionSignals.AlertReportedAt),
        new("identify-affected-users", "Identify affected user(s)", "Document accounts, users, mailboxes, or identities currently believed to be affected.", PlaybookAutoCompletionSignals.AffectedUsers),
        new("identify-affected-assets", "Identify affected asset(s)", "Document hosts, systems, services, repositories, data sets, or other assets in scope.", PlaybookAutoCompletionSignals.AffectedAssets),
        new("identify-involved-apps-tools", "Identify involved app(s) or tool(s)", "Document involved security tools, applications, platforms, or business systems.", PlaybookAutoCompletionSignals.InvolvedAppsOrTools),
        new("document-initial-findings", "Document initial findings", "Summarize the initial technical findings, confidence, impact, and scope assumptions.", PlaybookAutoCompletionSignals.InitialFindings),
        new("preserve-supporting-evidence", "Preserve supporting evidence", "Record evidence metadata for alerts, logs, messages, hashes, screenshots, or other supporting artifacts.", PlaybookAutoCompletionSignals.Evidence),
        new("document-ioc-summary", "Document IOC summary", "Summarize observed indicators such as IPs, domains, URLs, hashes, accounts, files, or process names.", PlaybookAutoCompletionSignals.IocSummary),
        new("document-containment-actions", "Document containment actions", "Document containment decisions such as isolation, blocking, session revocation, mailbox action, or access restriction.", PlaybookAutoCompletionSignals.ContainmentActions),
        new("provide-escalation-reason", "Provide escalation reason", "Document why higher-level review, ownership transfer, or management attention is required.", PlaybookAutoCompletionSignals.EscalationReason),
        new("prepare-closure-summary", "Prepare closure summary", "Summarize impact, actions taken, residual risk, and final closure rationale.", PlaybookAutoCompletionSignals.ClosureSummary)
    ];

    private static readonly PlaybookStepDefinition[] Phishing =
    [
        ..CommonFoundationalSteps,
        new("phishing-message-analysis", "Analyze message artifacts", "Review sender, recipients, headers, URLs, attachment names, and impersonation indicators."),
        new("phishing-scope-mailboxes", "Scope mailbox exposure", "Determine who received, opened, clicked, replied, or submitted credentials.")
    ];

    private static readonly PlaybookStepDefinition[] Malware =
    [
        ..CommonFoundationalSteps,
        new("malware-technical-analysis", "Perform malware technical analysis", "Review process, file, hash, persistence, command line, and related endpoint telemetry."),
        new("malware-eradication-recovery", "Plan eradication and recovery", "Document cleanup, reimage, credential, and monitoring follow-up decisions.")
    ];

    private static readonly PlaybookStepDefinition[] UnauthorizedAccess =
    [
        ..CommonFoundationalSteps,
        new("access-authentication-review", "Review authentication activity", "Analyze source IPs, session patterns, MFA events, privilege changes, and suspicious account behavior."),
        new("access-identity-recovery", "Plan identity recovery", "Document password reset, session revocation, token invalidation, access review, and monitoring follow-up.")
    ];

    private static readonly PlaybookStepDefinition[] Generic =
    [
        ..CommonFoundationalSteps,
        new("generic-technical-analysis", "Perform technical analysis", "Review available logs, alerts, observables, user reports, and timeline clues."),
        new("generic-recovery-followup", "Plan recovery follow-up", "Document recovery, monitoring, lessons learned, or handoff items for later closure.")
    ];
}
