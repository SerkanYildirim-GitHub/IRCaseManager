# IR Case Manager

## Overview

IR Case Manager is an early-stage incident response case management platform built with ASP.NET Core MVC and SQLite. It is designed to help security and IT teams track incident response cases, organize assignments, document evidence metadata, follow investigation playbooks, and maintain a structured case history.

## Purpose

Incident response work is often spread across tickets, spreadsheets, screenshots, email messages, chat conversations, and individual analyst notes. That fragmentation can make it difficult to understand what happened, who worked on the case, what evidence was reviewed, what actions were taken, and whether the case was properly closed.

IR Case Manager is being built to provide a focused workspace for internal incident response workflows, including:

* creating and tracking IR cases,
* assigning cases to analysts and teams,
* tracking case type, severity, status, and source reference,
* following investigation playbooks,
* documenting evidence metadata,
* supporting role-based and row-level case access,
* providing auditor visibility into closed cases,
* and building a foundation for future timeline, reporting, and MITRE ATT&CK mapping features.

## Product Stage

This project is in active MVP development. The current version demonstrates core case management workflows and foundational architecture. It is suitable for controlled development and evaluation, but it is not yet intended for production use with real sensitive incident data.

## Current Features

* ASP.NET Core MVC application structure
* SQLite data storage
* Local cookie-based authentication
* Role support for Admin, Analyst Level 2, Analyst Level 1, and Auditor
* Development-only seeded test accounts
* Dashboard case metrics
* Case creation workflow
* Case editing workflow
* Case queue with compact filtering
* Row-level case visibility enforcement
* Auditor read-only access to closed cases
* Case workspace
* Case-specific playbook sidebar
* Evidence metadata tracking
* Audit logging for key actions
* Dark mode support

## Security and Deployment Status

The current version is not production-ready for sensitive or regulated incident data.

Before production deployment, the platform will need additional hardening, including:

* account lockout and rate limiting,
* stronger password policy controls,
* production-safe user administration,
* secure production configuration,
* hardened audit logging,
* backup and recovery planning,
* secure file upload and storage design,
* production database planning,
* formal authorization review,
* and security testing.

Current security notes:

* Passwords are stored using ASP.NET Core password hashing.
* Global anti-forgery validation is enabled.
* Row-level case access is enforced for case visibility.
* Development test users are intended only for local development.
* Secrets should be supplied through environment variables or ignored local configuration, not committed files.
* Real incident data, production logs, credentials, API keys, tokens, customer data, and victim data should not be stored in this repository.

## Future Direction

Planned product capabilities include:

* admin user management,
* stronger login protection,
* response action tracking,
* investigation timeline reconstruction,
* MITRE ATT&CK mapping,
* structured incident reporting,
* secure evidence file handling,
* Windows Server Active Directory support for lab environments,
* future SSO support,
* connector architecture for security and IT tools,
* service-layer refactoring for case lifecycle, evidence, and playbooks,
* and deployment documentation.

## Developer Notes

This repository is a working application codebase. Review `.gitignore` before committing and ensure local database files, logs, private prompts, credentials, and environment-specific configuration are not committed.

Recommended next engineering priorities:

* add login lockout and failed-login auditing,
* improve production-safe user and role setup,
* refactor business logic into services as the application grows,
* harden cookie and deployment configuration,
* and plan the secure evidence upload architecture before adding file attachments.

## License

All rights reserved. 
