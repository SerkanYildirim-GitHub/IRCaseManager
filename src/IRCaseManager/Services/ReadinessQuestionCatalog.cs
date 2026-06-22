namespace IRCaseManager.Services;

public class ReadinessQuestionCatalog
{
    private static readonly IReadOnlyList<ReadinessQuestionDefinition> Questions =
    [
        new("Preparation", "preparation.security_strategy", "The organization has a cyber security policy or strategy covering prevention, preparedness, detection, response, recovery, review, and improvement.", 10),
        new("Preparation", "preparation.ir_plan_exists", "A cyber incident response plan has been developed.", 20),
        new("Preparation", "preparation.ir_plan_aligned", "The response plan aligns with business continuity, emergency management, and operating environment needs.", 30),
        new("Preparation", "preparation.ir_plan_tested", "The response plan has been reviewed or tested through an exercise.", 40),
        new("Preparation", "preparation.templates_ready", "Incident templates such as situation reports are prepared.", 50),
        new("Preparation", "preparation.staff_training", "Staff involved in managing incidents have received incident response training.", 60),
        new("Preparation", "preparation.offline_plan_access", "Hard-copy or offline versions of the response plan and playbooks are stored securely and are accessible to authorized staff.", 70),
        new("Preparation", "preparation.playbooks_exist", "Specific playbooks exist for common incident types.", 80),
        new("Preparation", "preparation.response_teams_defined", "A Cyber Incident Response Team and senior management response team, or equivalents, are defined with approved authority.", 90),
        new("Preparation", "preparation.sops_documented", "Relevant IT and OT standard operating procedures are documented and reviewed.", 100),
        new("Preparation", "preparation.provider_logging", "Service-provider, cloud, and SaaS logging and retention arrangements are established and tested.", 110),
        new("Preparation", "preparation.log_retention", "Log retention for critical systems is configured and tested.", 120),
        new("Preparation", "preparation.detection_capability", "Internal or third-party incident detection and analysis capability exists.", 130),
        new("Preparation", "preparation.critical_assets", "Critical data, applications, and systems are identified and documented.", 140),
        new("Preparation", "preparation.response_tools", "Incident logging, recordkeeping, and tracking tools are available and tested.", 150),
        new("Preparation", "preparation.role_cards", "Role cards or equivalent role descriptions exist for incident response participants.", 160),
        new("Preparation", "preparation.threat_monitoring", "Threat and situational awareness sources are monitored.", 170),

        new("Detection, Investigation, Analysis, and Activation", "detection.monitoring_sops", "SOPs exist for detection mechanisms such as scanning, sensors, and logging.", 210),
        new("Detection, Investigation, Analysis, and Activation", "detection.baseline_monitoring", "Network and user activity baselines are used to identify anomalous activity.", 220),
        new("Detection, Investigation, Analysis, and Activation", "detection.unauthorized_change_detection", "The organization can detect unauthorized hardware, software, or configuration changes.", 230),
        new("Detection, Investigation, Analysis, and Activation", "detection.sensitive_access_alerting", "Access to sensitive data and failed logon attempts are logged and alerted.", 240),
        new("Detection, Investigation, Analysis, and Activation", "detection.privileged_monitoring", "Privileged accounts receive enhanced monitoring.", 250),
        new("Detection, Investigation, Analysis, and Activation", "detection.external_notifications", "The organization can handle incident notifications from service providers, vendors, and trusted third parties.", 260),
        new("Detection, Investigation, Analysis, and Activation", "detection.categorization", "Incidents can be categorized, classified, and prioritized.", 270),
        new("Detection, Investigation, Analysis, and Activation", "detection.cirt_activation", "The Cyber Incident Response Team can be activated for critical incidents.", 280),
        new("Detection, Investigation, Analysis, and Activation", "detection.executive_activation", "Senior executive management can be activated for critical incidents.", 290),

        new("Containment, Evidence Collection, and Remediation", "containment.sops_playbooks", "SOPs, playbooks, and templates exist for containment, evidence collection, and remediation.", 310),
        new("Containment, Evidence Collection, and Remediation", "containment.evidence_storage", "A secure location exists for storing incident data and evidence.", 320),
        new("Containment, Evidence Collection, and Remediation", "containment.evidence_handling", "Evidence collection and handling procedures are documented.", 330),
        new("Containment, Evidence Collection, and Remediation", "containment.remediation_process", "Remediation procedures are documented and assigned.", 340),

        new("Communications", "communications.internal_external", "Internal and external communications plans, SOPs, and templates exist.", 410),
        new("Communications", "communications.media_process", "Public and media communications processes are documented.", 420),
        new("Communications", "communications.spokesperson", "A public/media spokesperson is assigned and supported by subject matter experts.", 430),
        new("Communications", "communications.training", "Staff responsible for communications are trained.", 440),
        new("Communications", "communications.general_staff_awareness", "Staff not directly involved in incident management understand how to handle inquiries and use approved messaging.", 450),

        new("Incident Notification and Reporting", "reporting.legal_regulatory", "Legal and regulatory incident notification/reporting processes and contact details are documented.", 510),
        new("Incident Notification and Reporting", "reporting.release_authority", "Authority and process for releasing or sharing incident information are documented.", 520),
        new("Incident Notification and Reporting", "reporting.insurance", "Insurance reporting requirements and processes are documented.", 530),

        new("Post-Incident Review", "postincident.review_process", "A post-incident review process is documented.", 610),
        new("Post-Incident Review", "postincident.recommendations", "Post-incident reports with recommendations are submitted to management for endorsement.", 620),
        new("Post-Incident Review", "postincident.action_register", "Follow-up actions from incidents or exercises are tracked to completion.", 630)
    ];

    public IReadOnlyList<ReadinessQuestionDefinition> GetQuestions()
    {
        return Questions;
    }
}

public sealed record ReadinessQuestionDefinition(
    string SectionName,
    string QuestionKey,
    string QuestionText,
    int DisplayOrder);
