# IR Case Manager

IR Case Manager is an ASP.NET Core MVC application for managing <b> Cybersecurity Incident Response</b># cases.

The project was shaped by a practical problem from day-to-day security operations: manually documenting incidents in Word files, maintaining folders, building timelines, and keeping naming conventions consistent across investigations. Over time, that process became difficult to manage, harder to search, and harder to scale.

IR Case Manager was created to bring more structure, consistency, and visibility to incident handling.

It is designed as a focused incident response case management tool rather than a general-purpose ticketing platform, with an emphasis on clear case ownership, investigation tracking, evidence-aware workflow, and readiness visibility.

## Purpose

IR Case Manager reflects a security-focused and operations-driven mindset centered on:

- Structured incident documentation
- Clear case ownership and handoff
- Consistent investigation tracking
- Organized case records and timelines
- Evidence-aware investigation workflow
- Readiness visibility and continuous improvement

The long-term direction is to shape it into a practical solution that can help smaller or budget-conscious organizations improve how they document, manage, and review incident response activity.

## Current Capabilities

The current application supports:

- Incident case creation and lifecycle tracking
- Assignment, reassignment, and escalation workflow
- Role-based case visibility
- Investigation workspace for case activity, timeline, and analyst notes
- Evidence metadata tracking
- Playbook progress tracking
- Dashboard visibility into case activity
- Auditor read-only access to closed cases
- IR Health Check for reviewing incident response readiness

## IR Health Check

IR Health Check helps review the current incident response readiness posture of a system, team, or organization across areas such as preparation, detection and analysis, containment and evidence, communications, notification and reporting, and post-incident review.

Its readiness model treats unanswered items as unverified gaps, supporting a cautious Zero Trust-style view of readiness.

## Technology

- ASP.NET Core MVC
- .NET 8
- Razor Views
- Entity Framework Core
- SQLite for local development
- Cookie-based authentication with application roles

## Authorization

The current role model includes:

- Admin
- Analyst L2
- Analyst L1
- Auditor

Core authorization and lifecycle behavior are documented in:

```text
docs/AUTHORIZATION.md
```

That file should be treated as the source of truth before changing authorization, escalation flow, closure behavior, or Auditor access.

## Build

From the repository root:

```powershell
dotnet build .\src\IRCaseManager\IRCaseManager.csproj
```

## Current Status

IR Case Manager is under active development and is currently being used as a focused project for workflow design, feature expansion, and practical incident response use-case development.

Additional work is planned around security hardening, authentication strategy, deployment preparation, monitoring, backup planning, and operational review.

## Roadmap

Planned and future areas of focus include:

- Expanded evidence management
- Secure evidence file upload
- MITRE ATT&CK mapping
- Reporting and export
- Playbook improvements
- AI-assisted features to support analyst workflow and audit review
- Security hardening
- Deployment preparation
- Active Directory authentication planning for VM lab use

## Project Direction

The goal is to build a practical incident response case management tool that improves structure, consistency, and visibility during cybersecurity investigations.

The project is being developed incrementally, with priority on useful workflow first and production hardening later.
