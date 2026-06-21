using System.ComponentModel.DataAnnotations;

namespace IRCaseManager.Models;

public enum CaseSeverity
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4,
    Informational = 5
}

public enum CaseStatus
{
    New = 1,
    Assigned = 2,
    Escalated = 3,
    Waiting = 4,
    Closed = 5
}

public enum DetectionSource
{
    EDR = 1,

    SIEM = 2,

    [Display(Name = "Email Gateway")]
    EmailGateway = 3,

    [Display(Name = "User Report")]
    UserReport = 4,

    Helpdesk = 5,

    [Display(Name = "Network Alert")]
    NetworkAlert = 6,

    [Display(Name = "Threat Intelligence")]
    ThreatIntelligence = 7,

    Other = 8
}

public enum CaseType
{
    [Display(Name = "Alert Triage")]
    AlertTriage = 1,

    [Display(Name = "Malware")]
    Malware = 2,

    [Display(Name = "Phishing")]
    Phishing = 3,

    [Display(Name = "Unauthorized Access")]
    UnauthorizedAccess = 4,

    [Display(Name = "Data Exposure")]
    DataExposure = 5,

    [Display(Name = "Other")]
    Other = 6
}
