# IR Case Manager

## Goal

IR Case Manager is an early-stage incident response case management platform designed to help security teams create, assign, investigate, escalate, close, and review cybersecurity incident cases.

The project is focused on a practical, security-first incident response workflow rather than general-purpose ticketing.

## Current Status

This is an MVP under active development and is not production-ready.

The current build focuses on secure case lifecycle handling, role-aware access, investigation workflow structure, and field-driven playbook progress.

## What It Can Do Now

* Dashboard overview for case metrics
* Create and track incident response cases
* Auto-generate case IDs and case titles
* Capture source references such as TK references
* Assign cases by team and analyst
* Enforce role-based and case-level access controls
* Support secure case assignment and escalation handoff
* Preserve assignment history for chain-of-custody visibility
* Allow previous owners to retain view-only access after escalation
* Restrict closed-case changes to authorized users
* Provide Auditor read-only access to closed cases
* Filter and review case records
* Edit open case details with access controls
* Capture structured investigation details, including:

  * detection source
  * alert/report time
  * affected users
  * affected assets
  * involved apps or tools
  * initial findings
  * IOC summary
  * containment actions
  * escalation reason
  * closure summary
* Auto-complete playbook steps from investigation data
* Block escalation until minimum investigation details are complete
* Block closure until required containment and closure details are complete
* Track evidence metadata
* Provide quick resource links such as CISA KEV
* Audit key authentication and case lifecycle activity
* Apply failed-login tracking, account lockout, and login rate limiting

## Incident Response Direction

The investigation workspace and playbook structure are being aligned conceptually with:

* NIST SP 800-61r3
* NIST Cybersecurity Framework 2.0
* CISA Cybersecurity Incident & Vulnerability Response Playbooks
* CRR Incident Management guidance

The goal is to support a practical workflow across detection, analysis, containment, escalation, recovery, closure, and later reporting.

## Coming Soon

* Case activity timeline and investigation log
* Improved evidence workflow
* IOC tracking improvements
* MITRE ATT&CK mapping
* CISA KEV enrichment
* Export and reporting features
* Management metrics and audit review screens
* Production database, migration, backup, and deployment hardening
* Customer/company branding options

## Technology

* ASP.NET Core MVC
* SQLite
* Entity Framework Core
* Razor views

## Note

SQLite is currently used for development and MVP testing. Production use will require proper database migrations, backup planning, deployment hardening, and operational security review.
