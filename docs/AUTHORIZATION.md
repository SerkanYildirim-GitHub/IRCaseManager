# Authorization and Case Lifecycle

## Purpose

This document defines the Phase 1 secure case lifecycle and role authorization rules for IR Case Manager. It serves as the authoritative reference for access control decisions and case workflow progression to prevent future development from inadvertently weakening the security model.

## Role Hierarchy

Roles form a linear escalation hierarchy:

- **L1 (Analyst Level 1)** — Entry-level incident responders
- **L2 (Analyst Level 2)** — Senior incident responders  
- **Admin** — Administrative authority; highest escalation level

**Auditor** is read-only and outside the escalation chain. Auditors do not participate in escalation and cannot create or modify cases.

## Case Lifecycle States

Cases progress through the following states:

| State | Meaning |
|-------|---------|
| **New** | Newly created; not yet assigned |
| **Assigned** | Assigned to an analyst (L1 or L2) or team |
| **Escalated** | Handed off to a higher authority; awaiting acceptance or review |
| **Waiting** | Awaiting external action, evidence, or decision |
| **Closed** | Investigation concluded; no further work expected |

## Role Capabilities

| Capability | L1 | L2 | Admin | Auditor |
|---|:---:|:---:|:---:|:---:|
| Create cases | ✓ | ✓ | ✓ | |
| Edit open cases they created/own | ✓ | ✓ | ✓ | |
| Edit open cases assigned to them | ✓ | ✓ | ✓ | |
| Close open cases they own/are assigned | ✓ | ✓ | ✓ | |
| Edit closed cases | | | ✓ | |
| Reopen closed cases | | | ✓ | |
| Escalate owned/assigned cases | ✓* | ✓* | | |
| View closed cases | | | ✓ | ✓ |
| View cases in assignment history | ✓ | ✓ | ✓ | |
| Add/modify evidence | ✓ | ✓ | ✓ | |
| Modify playbook steps | ✓ | ✓ | ✓ | |

*L1 escalates to L2; L2 escalates to Admin. Admin does not escalate (highest authority).

## Escalation Handoff Rules

Escalation is a formal handoff of case ownership with full chain-of-custody tracking:

1. **Escalation Source**
   - L1 can escalate cases assigned to or created by them
   - L2 can escalate cases assigned to or created by them
   - Admin cannot escalate (highest authority)

2. **Escalation Target**
   - L1 escalates to L2 (case assigned to active L2 user)
   - L2 escalates to Admin (case assigned to active Admin user)
   - Escalation fails if a unique active user in the target role cannot be determined

3. **Ownership Transfer**
   - Case ownership transfers to the active user in the target role
   - Previous owner(s) retain view-only access through assignment history
   - Previous owners cannot edit or modify the case after escalation
   - Case may be viewed by anyone in assignment history (chain of custody transparency)

4. **Audit Trail**
   - `CaseAssignmentHistory` records: who escalated, from whom, to whom, when, and reason (if provided)
   - History is immutable and persisted for regulatory compliance

## Closed Case Rules

- **L1/L2**: Cannot edit or reopen closed cases
- **Admin**: Can edit or reopen closed cases  
- **Auditor**: Can view closed cases but cannot modify them

## Server-Side Authorization

Security is enforced at the controller/service layer, not in the UI:

- UI hiding is not a security boundary; it is a convenience feature only
- All state-modifying actions (POST/PUT/DELETE) must validate authorization on the server before changing data
- `CaseAccessService` is the single source of truth for case access decisions
- Views should consume pre-computed authorization flags from view models (e.g., `Model.CanCloseCase`) rather than duplicating role logic in templates
- Legacy views with view-layer authorization (`User.IsInRole()` checks) should be refactored or removed to prevent authorization bypass

## Chain of Custody

- **Assignment History**: Every case creation, assignment, reassignment, and escalation is recorded
- **Participant Visibility**: Users who participated in a case's history can view it after handoff
- **History Integrity**: Assignment records are immutable and include:
  - Action type (Created, Assigned, Reassigned, Escalated)
  - From user/team
  - To user/team  
  - Performed by user
  - Reason (if provided)
  - Timestamp (UTC)

## Current MVP Constraints

- **Single Active User per Role**: Development currently assumes one active L2 user and one active Admin user for deterministic escalation handoff. Multi-user deployments may require escalation queue/approval logic.
- **Development Schema**: SQLite schema maintenance in `SeedData.cs` (manual `ALTER TABLE`) is for development use only. Production deployments must use Entity Framework Core migrations.
- **No Audit Export**: Current audit logs are stored in the database but do not have an export/reporting interface.

## Reference Implementation

Authorization decisions are centralized in:
- `CaseAccessService` — Case visibility and modification rules
- `CaseAccessService.FilterVisibleCases()` — Role-based case filtering
- `CaseAccessService.CanEditCase()`, `CanCloseCase()`, `CanReopenCase()`, `CanEscalateCase()` — Permission checks
- `CaseWorkspaceViewModel` — Pre-computed authorization flags for the active view

Before implementing new case operations or authorization logic, consult these services to avoid introducing weaker patterns.
