using IRCaseManager.Data;
using IRCaseManager.Extensions;
using IRCaseManager.Models;
using IRCaseManager.Security;
using IRCaseManager.Services;
using IRCaseManager.ViewModels;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class CasesController(
    AppDbContext db,
    CaseIdGenerator caseIdGenerator,
    PlaybookDefinitionService playbookDefinitionService,
    CaseAccessService caseAccessService) : Controller
{
    private const string IncidentResponseQueue = "Incident Response";
    private const string ItSupportQueue = "IT Support";
    private const string AdminReviewQueue = "Admin Review";

    public async Task<IActionResult> Index()
    {
        var visibleCaseSet = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Include(irCase => irCase.CreatedBy)
            .Include(irCase => irCase.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
                    .ThenInclude(applicationUser => applicationUser!.Role)
            .ToListAsync();

        var cases = visibleCaseSet
            .OrderByDescending(irCase => irCase.OpenedAt)
            .ToList();

        return View(cases);
    }

    [HttpGet]
    public async Task<IActionResult> MyCases()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var visibleCaseSet = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Where(irCase => irCase.Assignments.Any(assignment => assignment.ApplicationUserId == userId.Value))
            .Include(irCase => irCase.CreatedBy)
            .Include(irCase => irCase.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
                    .ThenInclude(applicationUser => applicationUser!.Role)
            .ToListAsync();

        var cases = visibleCaseSet
            .OrderByDescending(irCase => irCase.OpenedAt)
            .ToList();

        return View(cases);
    }

    [HttpGet]
    public async Task<IActionResult> Analytics(string? range, string? from, string? to)
    {
        var selectedRange = BuildAnalyticsRange(range, from, to);
        var visibleCases = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Include(irCase => irCase.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
                    .ThenInclude(applicationUser => applicationUser!.Role)
            .ToListAsync();

        var cases = visibleCases
            .Where(irCase => irCase.OpenedAt >= selectedRange.StartUtc && irCase.OpenedAt < selectedRange.EndExclusiveUtc)
            .ToList();

        var model = BuildAnalyticsViewModel(cases, selectedRange);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Include(caseRecord => caseRecord.CreatedBy)
            .Include(caseRecord => caseRecord.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
                    .ThenInclude(applicationUser => applicationUser!.Role)
            .Include(caseRecord => caseRecord.EvidenceItems)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .Include(caseRecord => caseRecord.TimelineEntries)
                .ThenInclude(entry => entry.CreatedBy)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.FromUser)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.ToUser)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.PerformedByUser)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        return View("DetailsWorkspace", BuildWorkspaceViewModel(irCase));
    }

    [Authorize(Policy = AuthorizationPolicies.CanCreateCases)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCreateCaseOptionsAsync();
        return View(new CreateCaseViewModel());
    }

    [Authorize(Policy = AuthorizationPolicies.CanCreateCases)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCaseViewModel model)
    {
        model.Trim();

        if (!ModelState.IsValid)
        {
            await PopulateCreateCaseOptionsAsync();
            return View(model);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        ApplicationUser? assignedUser = null;
        if (model.AssignedUserId is not null)
        {
            assignedUser = await GetAssignableCaseUserAsync(model.AssignedUserId.Value);
            if (assignedUser is null)
            {
                ModelState.AddModelError(nameof(model.AssignedUserId), "Select a valid assignable user.");
                await PopulateCreateCaseOptionsAsync();
                return View(model);
            }
        }

        var openedAt = DateTimeOffset.UtcNow;
        var caseId = await caseIdGenerator.GenerateAsync(openedAt);
        var sourceReference = GenerateSourceReference(caseId);
        var caseTypeName = model.CaseType!.Value.GetDisplayName();
        var assignedQueue = assignedUser is null ? model.AssignedTeam : GetQueueForAssignedUser(assignedUser);
        var irCase = new Case
        {
            CaseId = caseId,
            Title = $"{caseId} - {caseTypeName}",
            Severity = model.Severity!.Value,
            CaseType = model.CaseType!.Value,
            Status = model.AssignedUserId is null ? CaseStatus.New : CaseStatus.Assigned,
            SourceReference = sourceReference,
            AssignedTeam = assignedQueue,
            OpenedAt = openedAt,
            InitialSummary = model.InitialSummary,
            Visibility = "Default",
            CreatedById = userId.Value,
            CreatedAt = openedAt
        };

        db.Cases.Add(irCase);

        if (model.AssignedUserId is not null)
        {
            db.CaseAssignments.Add(new CaseAssignment
            {
                Case = irCase,
                ApplicationUserId = model.AssignedUserId.Value,
                AssignedById = userId.Value,
                AssignedAt = openedAt
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "CaseCreated",
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = "Case record created."
        });

        AddAssignmentHistory(
            irCase,
            model.AssignedUserId is null ? "Created" : "Assigned",
            fromUserId: null,
            fromTeam: null,
            toUserId: model.AssignedUserId,
            toTeam: assignedQueue,
            performedByUserId: userId.Value,
            reason: model.AssignedUserId is null ? "Case created without an assigned analyst." : "Case created and assigned.",
            occurredUtc: openedAt);

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Created {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanEditCase(irCase, User))
        {
            return Forbid();
        }

        await PopulateCreateCaseOptionsAsync();
        return View(new EditCaseViewModel
        {
            CaseId = irCase.CaseId,
            SourceReference = irCase.SourceReference,
            CaseType = irCase.CaseType,
            Severity = irCase.Severity,
            AssignedTeam = irCase.AssignedTeam,
            AssignedUserId = irCase.Assignments
                .OrderByDescending(assignment => assignment.AssignedAt)
                .Select(assignment => (int?)assignment.ApplicationUserId)
                .FirstOrDefault(),
            InitialSummary = irCase.InitialSummary
        });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> Edit(string id, EditCaseViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        model.Trim();

        if (!string.Equals(id, model.CaseId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanEditCase(irCase, User))
        {
            return Forbid();
        }

        ApplicationUser? assignedUser = null;
        if (model.AssignedUserId is not null)
        {
            assignedUser = await GetAssignableCaseUserAsync(model.AssignedUserId.Value);
            if (assignedUser is null)
            {
                ModelState.AddModelError(nameof(model.AssignedUserId), "Select a valid assignable user.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.SourceReference = irCase.SourceReference;
            await PopulateCreateCaseOptionsAsync();
            return View(model);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        var previousAssignedUserId = GetCurrentAssignedUserId(irCase);
        var previousAssignedTeam = irCase.AssignedTeam;
        var caseTypeName = model.CaseType!.Value.GetDisplayName();
        var assignedQueue = assignedUser is null ? model.AssignedTeam : GetQueueForAssignedUser(assignedUser);
        irCase.CaseType = model.CaseType.Value;
        irCase.Title = $"{irCase.CaseId} - {caseTypeName}";
        irCase.Severity = model.Severity!.Value;
        irCase.AssignedTeam = assignedQueue;
        irCase.InitialSummary = model.InitialSummary;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        var selectedAssignedUserId = assignedUser?.Id;
        foreach (var assignment in irCase.Assignments.Where(assignment => assignment.ApplicationUserId != selectedAssignedUserId).ToList())
        {
            db.CaseAssignments.Remove(assignment);
        }

        if (selectedAssignedUserId is not null &&
            !irCase.Assignments.Any(assignment => assignment.ApplicationUserId == selectedAssignedUserId.Value))
        {
            db.CaseAssignments.Add(new CaseAssignment
            {
                CaseId = irCase.Id,
                ApplicationUserId = selectedAssignedUserId.Value,
                AssignedById = userId.Value,
                AssignedAt = now
            });
        }

        var assignmentChanged = previousAssignedUserId != selectedAssignedUserId ||
            !string.Equals(previousAssignedTeam, assignedQueue, StringComparison.Ordinal);
        if (assignmentChanged)
        {
            AddAssignmentHistory(
                irCase,
                previousAssignedUserId is null && selectedAssignedUserId is not null ? "Assigned" : "Reassigned",
                previousAssignedUserId,
                previousAssignedTeam,
                selectedAssignedUserId,
                assignedQueue,
                userId.Value,
                "Case assignment updated.",
                now);
        }

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "CaseUpdated",
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = "Basic case information updated."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Updated {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> Close(string id)
    {
        var updateResult = await UpdateCaseLifecycleAsync(
            id,
            CaseStatus.Closed,
            closedAt: DateTimeOffset.UtcNow,
            action: "CaseClosed",
            summary: "Case closed.",
            canUpdate: irCase => caseAccessService.CanCloseCase(irCase, User),
            validate: GetMissingCloseRequirements);

        return updateResult;
    }

    [Authorize(Policy = AuthorizationPolicies.CanReopenCases)]
    [HttpPost]
    public async Task<IActionResult> Reopen(string id)
    {
        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanReopenCase(irCase, User))
        {
            return Forbid();
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        irCase.Status = irCase.Assignments.Count == 0 ? CaseStatus.New : CaseStatus.Assigned;
        irCase.ClosedAt = null;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "CaseReopened",
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = "Case reopened."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Reopened {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> Escalate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanEscalateCase(irCase, User))
        {
            return Forbid();
        }

        var missingFields = GetMissingEscalationRequirements(irCase);
        if (missingFields.Count > 0)
        {
            TempData["StatusMessage"] = BuildMissingFieldsMessage("Case was not escalated", missingFields);
            return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var targetRoleName = GetEscalationTargetRoleName();
        if (targetRoleName is null)
        {
            return Forbid();
        }

        var targetUser = await GetSingleActiveUserForRoleAsync(targetRoleName);
        if (targetUser is null)
        {
            TempData["StatusMessage"] = $"Unable to identify exactly one active {targetRoleName} user. Case was not escalated.";
            return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
        }

        var now = DateTimeOffset.UtcNow;
        var previousAssignedUserId = GetCurrentAssignedUserId(irCase);
        var previousAssignedTeam = irCase.AssignedTeam;
        var targetQueue = GetQueueForAssignedUser(targetUser);

        foreach (var assignment in irCase.Assignments.ToList())
        {
            db.CaseAssignments.Remove(assignment);
        }

        db.CaseAssignments.Add(new CaseAssignment
        {
            CaseId = irCase.Id,
            ApplicationUserId = targetUser.Id,
            AssignedById = userId.Value,
            AssignedAt = now
        });

        irCase.Status = CaseStatus.Escalated;
        irCase.AssignedTeam = targetQueue;
        irCase.ClosedAt = null;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        AddAssignmentHistory(
            irCase,
            "Escalated",
            previousAssignedUserId,
            previousAssignedTeam,
            targetUser.Id,
            targetQueue,
            userId.Value,
            $"Case escalated to {targetRoleName}.",
            now);

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "CaseEscalated",
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = $"Case escalated to {targetRoleName}."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Escalated {irCase.CaseId} to {targetRoleName}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> TogglePlaybookStep(string id, string stepKey, string? completionState)
    {
        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .Include(caseRecord => caseRecord.EvidenceItems)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanModifyPlaybook(irCase, User))
        {
            return Forbid();
        }

        var definition = playbookDefinitionService
            .GetSteps(irCase.CaseType)
            .SingleOrDefault(step => step.Key == stepKey);

        if (definition is null)
        {
            return NotFound();
        }

        if (IsAutoManagedStep(definition))
        {
            TempData["StatusMessage"] = "This playbook step is managed by case data. Update the related investigation field to complete it.";
            return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        var completion = irCase.PlaybookSteps.SingleOrDefault(step => step.StepKey == stepKey);
        if (completion is null)
        {
            completion = new CasePlaybookStep
            {
                CaseId = irCase.Id,
                StepKey = stepKey,
                UpdatedById = userId.Value
            };
            db.CasePlaybookSteps.Add(completion);
        }

        var isCompleted = string.Equals(completionState, "complete", StringComparison.OrdinalIgnoreCase);
        completion.IsCompleted = isCompleted;
        completion.CompletedAt = isCompleted ? now : null;
        completion.CompletedById = isCompleted ? userId.Value : null;
        completion.UpdatedAt = now;
        completion.UpdatedById = userId.Value;

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = isCompleted ? "PlaybookStepCompleted" : "PlaybookStepReopened",
            EntityType = nameof(CasePlaybookStep),
            EntityId = $"{irCase.CaseId}:{stepKey}",
            Summary = isCompleted ? "Playbook step marked complete." : "Playbook step marked incomplete."
        });

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> AddEvidence(string id, EvidenceMetadataViewModel model)
    {
        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanModifyEvidence(irCase, User))
        {
            return Forbid();
        }

        model.Trim();

        if (!ModelState.IsValid)
        {
            var reloadedCase = await caseAccessService
                .FilterVisibleCases(db.Cases.AsNoTracking(), User)
                .Include(caseRecord => caseRecord.CreatedBy)
                .Include(caseRecord => caseRecord.Assignments)
                    .ThenInclude(assignment => assignment.ApplicationUser)
                        .ThenInclude(applicationUser => applicationUser!.Role)
                .Include(caseRecord => caseRecord.EvidenceItems)
                .Include(caseRecord => caseRecord.PlaybookSteps)
                .Include(caseRecord => caseRecord.TimelineEntries)
                    .ThenInclude(entry => entry.CreatedBy)
                .Include(caseRecord => caseRecord.AssignmentHistory)
                    .ThenInclude(history => history.FromUser)
                .Include(caseRecord => caseRecord.AssignmentHistory)
                    .ThenInclude(history => history.ToUser)
                .Include(caseRecord => caseRecord.AssignmentHistory)
                    .ThenInclude(history => history.PerformedByUser)
                .SingleAsync(caseRecord => caseRecord.CaseId == id);

            var workspace = BuildWorkspaceViewModel(reloadedCase, model);
            return View("DetailsWorkspace", workspace);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var evidence = new EvidenceMetadata
        {
            CaseId = irCase.Id,
            EvidenceType = model.EvidenceType,
            Name = model.Name,
            Description = model.Description,
            Source = model.Source,
            AnalystNotes = model.AnalystNotes,
            CollectedAt = model.CollectedAt!.Value.ToUniversalTime(),
            CollectedById = userId.Value
        };

        db.EvidenceMetadata.Add(evidence);
        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "EvidenceMetadataAdded",
            EntityType = nameof(EvidenceMetadata),
            EntityId = irCase.CaseId,
            Summary = "Evidence metadata added."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Evidence added to {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> AddTimelineEntry(
        string id,
        [Bind(Prefix = "NewTimelineEntry")] TimelineEntryViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanModifyEvidence(irCase, User))
        {
            return Forbid();
        }

        model.Trim();

        if (!ModelState.IsValid)
        {
            var reloadedCase = await LoadCaseForWorkspaceAsync(id, asNoTracking: true);
            if (reloadedCase is null)
            {
                return await CaseNotFoundOrForbiddenAsync(id);
            }

            return View("DetailsWorkspace", BuildWorkspaceViewModel(reloadedCase, newTimelineEntry: model));
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        var timelineEntry = new TimelineEntry
        {
            CaseId = irCase.Id,
            EventTime = now,
            Title = model.ActivityType,
            Description = model.Body,
            Source = "Manual",
            CreatedById = userId.Value,
            CreatedAt = now
        };

        db.TimelineEntries.Add(timelineEntry);
        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "TimelineEntryAdded",
            EntityType = nameof(TimelineEntry),
            EntityId = irCase.CaseId,
            Summary = "Case timeline entry added."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Timeline entry added to {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    [Authorize(Policy = AuthorizationPolicies.CanEditCases)]
    [HttpPost]
    public async Task<IActionResult> UpdateInvestigationDetails(
        string id,
        [Bind(Prefix = "InvestigationDetails")] InvestigationDetailsViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!caseAccessService.CanModifyPlaybook(irCase, User))
        {
            return Forbid();
        }

        model.Trim();

        if (!ModelState.IsValid)
        {
            var reloadedCase = await LoadCaseForWorkspaceAsync(id, asNoTracking: true);
            if (reloadedCase is null)
            {
                return await CaseNotFoundOrForbiddenAsync(id);
            }

            return View("DetailsWorkspace", BuildWorkspaceViewModel(reloadedCase, investigationDetails: model));
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        irCase.DetectionSource = model.DetectionSource;
        irCase.AlertReportedAtUtc = model.AlertReportedAtUtc?.ToUniversalTime();
        irCase.AffectedUsers = model.AffectedUsers;
        irCase.AffectedAssets = model.AffectedAssets;
        irCase.InvolvedAppsOrTools = model.InvolvedAppsOrTools;
        irCase.InitialFindings = model.InitialFindings;
        irCase.ContainmentActions = model.ContainmentActions;
        irCase.IocSummary = model.IocSummary;
        irCase.EscalationReason = model.EscalationReason;
        irCase.ClosureSummary = model.ClosureSummary;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = "InvestigationDetailsUpdated",
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = "Investigation details updated."
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Investigation details updated for {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    private async Task PopulateAssignedUserOptionsAsync()
    {
        var users = await db.Users
            .AsNoTracking()
            .Where(applicationUser => applicationUser.IsActive)
            .OrderBy(applicationUser => applicationUser.UserName)
            .Select(applicationUser => new SelectListItem
            {
                Value = applicationUser.Id.ToString(),
                Text = applicationUser.UserName
            })
            .ToListAsync();

        ViewBag.AssignedUserOptions = users;
    }

    private async Task PopulateCreateCaseOptionsAsync()
    {
        ViewBag.AssignedTeamOptions = new List<SelectListItem>
        {
            new() { Value = IncidentResponseQueue, Text = IncidentResponseQueue },
            new() { Value = ItSupportQueue, Text = ItSupportQueue },
            new() { Value = AdminReviewQueue, Text = AdminReviewQueue }
        };

        var users = await db.Users
            .AsNoTracking()
            .Include(applicationUser => applicationUser.Role)
            .Where(applicationUser =>
                applicationUser.IsActive &&
                applicationUser.Role != null &&
                (applicationUser.Role.Name == RoleNames.AnalystLevel1 ||
                 applicationUser.Role.Name == RoleNames.AnalystLevel2 ||
                 applicationUser.Role.Name == RoleNames.Admin))
            .OrderBy(applicationUser => applicationUser.UserName)
            .Select(applicationUser => new
            {
                applicationUser.Id,
                applicationUser.UserName,
                RoleName = applicationUser.Role!.Name
            })
            .ToListAsync();

        ViewBag.AssignedUserOptions = users
            .Select(applicationUser => new SelectListItem
            {
                Value = applicationUser.Id.ToString(),
                Text = applicationUser.UserName
            })
            .ToList();
    }

    private async Task<ApplicationUser?> GetAssignableCaseUserAsync(int userId)
    {
        return await db.Users
            .Include(applicationUser => applicationUser.Role)
            .SingleOrDefaultAsync(applicationUser =>
                applicationUser.Id == userId &&
                applicationUser.IsActive &&
                applicationUser.Role != null &&
                (applicationUser.Role.Name == RoleNames.AnalystLevel1 ||
                 applicationUser.Role.Name == RoleNames.AnalystLevel2 ||
                 applicationUser.Role.Name == RoleNames.Admin));
    }

    private static string GetQueueForAssignedUser(ApplicationUser applicationUser)
    {
        return applicationUser.Role?.Name switch
        {
            RoleNames.Admin => AdminReviewQueue,
            _ => IncidentResponseQueue
        };
    }

    private static string GenerateSourceReference(string caseId)
    {
        var sequence = caseId.Split('-').LastOrDefault();
        return int.TryParse(sequence, out var number) ? $"TK-{number:00000}" : "TK-00000";
    }

    private async Task<Case?> LoadCaseForWorkspaceAsync(string id, bool asNoTracking)
    {
        var cases = asNoTracking ? db.Cases.AsNoTracking() : db.Cases;

        return await caseAccessService
            .FilterVisibleCases(cases, User)
            .Include(caseRecord => caseRecord.CreatedBy)
            .Include(caseRecord => caseRecord.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
                    .ThenInclude(applicationUser => applicationUser!.Role)
            .Include(caseRecord => caseRecord.EvidenceItems)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .Include(caseRecord => caseRecord.TimelineEntries)
                .ThenInclude(entry => entry.CreatedBy)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.FromUser)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.ToUser)
            .Include(caseRecord => caseRecord.AssignmentHistory)
                .ThenInclude(history => history.PerformedByUser)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
    }

    private string? GetEscalationTargetRoleName()
    {
        if (User.IsInRole(RoleNames.AnalystLevel1))
        {
            return RoleNames.AnalystLevel2;
        }

        if (User.IsInRole(RoleNames.AnalystLevel2))
        {
            return RoleNames.Admin;
        }

        return null;
    }

    private async Task<ApplicationUser?> GetSingleActiveUserForRoleAsync(string roleName)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(applicationUser => applicationUser.Role)
            .Where(applicationUser =>
                applicationUser.IsActive &&
                applicationUser.Role != null &&
                applicationUser.Role.Name == roleName)
            .OrderBy(applicationUser => applicationUser.Id)
            .Take(2)
            .ToListAsync();

        return users.Count == 1 ? users[0] : null;
    }

    private static int? GetCurrentAssignedUserId(Case irCase)
    {
        return irCase.Assignments
            .OrderByDescending(assignment => assignment.AssignedAt)
            .Select(assignment => (int?)assignment.ApplicationUserId)
            .FirstOrDefault();
    }

    private void AddAssignmentHistory(
        Case irCase,
        string actionType,
        int? fromUserId,
        string? fromTeam,
        int? toUserId,
        string? toTeam,
        int performedByUserId,
        string reason,
        DateTimeOffset occurredUtc)
    {
        var history = new CaseAssignmentHistory
        {
            ActionType = actionType,
            FromUserId = fromUserId,
            FromTeam = fromTeam,
            ToUserId = toUserId,
            ToTeam = toTeam,
            PerformedByUserId = performedByUserId,
            Reason = reason,
            OccurredUtc = occurredUtc
        };

        if (irCase.Id == 0)
        {
            history.Case = irCase;
        }
        else
        {
            history.CaseId = irCase.Id;
        }

        db.CaseAssignmentHistories.Add(history);
    }

    private static IReadOnlyList<string> GetMissingEscalationRequirements(Case irCase)
    {
        var missing = new List<string>();
        AddIfMissing(missing, irCase.DetectionSource is not null, "Detection source");
        AddIfMissing(missing, irCase.AlertReportedAtUtc is not null, "Alert/report time");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.AffectedUsers), "Affected user(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.AffectedAssets), "Affected asset(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.InvolvedAppsOrTools), "Involved app(s) or tool(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.InitialFindings), "Initial findings");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.EscalationReason), "Escalation reason");
        return missing;
    }

    private static IReadOnlyList<string> GetMissingCloseRequirements(Case irCase)
    {
        var missing = new List<string>();
        AddIfMissing(missing, irCase.DetectionSource is not null, "Detection source");
        AddIfMissing(missing, irCase.AlertReportedAtUtc is not null, "Alert/report time");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.AffectedUsers), "Affected user(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.AffectedAssets), "Affected asset(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.InvolvedAppsOrTools), "Involved app(s) or tool(s)");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.InitialFindings), "Initial findings");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.ContainmentActions), "Containment actions");
        AddIfMissing(missing, !string.IsNullOrWhiteSpace(irCase.ClosureSummary), "Closure summary");
        return missing;
    }

    private static void AddIfMissing(ICollection<string> missing, bool hasValue, string label)
    {
        if (!hasValue)
        {
            missing.Add(label);
        }
    }

    private static string BuildMissingFieldsMessage(string prefix, IReadOnlyList<string> missingFields)
    {
        return $"{prefix}. Complete required investigation fields first: {string.Join(", ", missingFields)}.";
    }

    private sealed record AnalyticsRange(
        string Key,
        string Label,
        DateTimeOffset StartUtc,
        DateTimeOffset EndExclusiveUtc,
        bool GroupByHour,
        string? FromInput,
        string? ToInput,
        string? ValidationMessage);

    private sealed record AnalyticsBreakdownSeed(string Label, int Count);

    private static AnalyticsRange BuildAnalyticsRange(string? range, string? from, string? to)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedRange = string.IsNullOrWhiteSpace(range) ? "7d" : range.Trim().ToLowerInvariant();

        return normalizedRange switch
        {
            "24h" => new AnalyticsRange(
                "24h",
                "Last 24 Hours",
                now.AddHours(-24),
                now,
                GroupByHour: true,
                FromInput: null,
                ToInput: null,
                ValidationMessage: null),
            "30d" => new AnalyticsRange(
                "30d",
                "Last 30 Days",
                now.AddDays(-30),
                now,
                GroupByHour: false,
                FromInput: null,
                ToInput: null,
                ValidationMessage: null),
            "custom" => BuildCustomAnalyticsRange(from, to, now),
            _ => new AnalyticsRange(
                "7d",
                "Last 7 Days",
                now.AddDays(-7),
                now,
                GroupByHour: false,
                FromInput: null,
                ToInput: null,
                ValidationMessage: normalizedRange == "7d" ? null : "Invalid range. Showing Last 7 Days.")
        };
    }

    private static AnalyticsRange BuildCustomAnalyticsRange(string? from, string? to, DateTimeOffset now)
    {
        if (!TryParseAnalyticsDate(from, out var fromDate) ||
            !TryParseAnalyticsDate(to, out var toDate) ||
            toDate < fromDate)
        {
            return new AnalyticsRange(
                "7d",
                "Last 7 Days",
                now.AddDays(-7),
                now,
                GroupByHour: false,
                FromInput: from,
                ToInput: to,
                ValidationMessage: "Invalid custom range. Showing Last 7 Days.");
        }

        var startUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endExclusiveUtc = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return new AnalyticsRange(
            "custom",
            $"Custom Range: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
            startUtc,
            endExclusiveUtc,
            GroupByHour: false,
            FromInput: fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToInput: toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ValidationMessage: null);
    }

    private static bool TryParseAnalyticsDate(string? value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static CaseAnalyticsViewModel BuildAnalyticsViewModel(IReadOnlyList<Case> cases, AnalyticsRange range)
    {
        return new CaseAnalyticsViewModel
        {
            SelectedRange = range.Key,
            RangeLabel = range.Label,
            FromInput = range.FromInput,
            ToInput = range.ToInput,
            ValidationMessage = range.ValidationMessage,
            TotalCases = cases.Count,
            OpenCases = cases.Count(irCase => irCase.Status != CaseStatus.Closed),
            ClosedCases = cases.Count(irCase => irCase.Status == CaseStatus.Closed),
            EscalatedCases = cases.Count(irCase => irCase.Status == CaseStatus.Escalated),
            CriticalCases = cases.Count(irCase => irCase.Severity == CaseSeverity.Critical),
            HighCases = cases.Count(irCase => irCase.Severity == CaseSeverity.High),
            SeverityBreakdown = BuildBreakdown([
                new("Critical", cases.Count(irCase => irCase.Severity == CaseSeverity.Critical)),
                new("High", cases.Count(irCase => irCase.Severity == CaseSeverity.High)),
                new("Medium", cases.Count(irCase => irCase.Severity == CaseSeverity.Medium)),
                new("Low", cases.Count(irCase => irCase.Severity == CaseSeverity.Low))
            ]),
            StatusBreakdown = BuildBreakdown([
                new("New", cases.Count(irCase => irCase.Status == CaseStatus.New)),
                new("Assigned", cases.Count(irCase => irCase.Status == CaseStatus.Assigned)),
                new("Escalated", cases.Count(irCase => irCase.Status == CaseStatus.Escalated)),
                new("Waiting", cases.Count(irCase => irCase.Status == CaseStatus.Waiting)),
                new("Closed", cases.Count(irCase => irCase.Status == CaseStatus.Closed))
            ]),
            CaseTypeBreakdown = BuildBreakdown(Enum.GetValues<CaseType>()
                .Select(caseType => new AnalyticsBreakdownSeed(
                    caseType.GetDisplayName(),
                    cases.Count(irCase => irCase.CaseType == caseType)))),
            QueueBreakdown = BuildQueueBreakdown(cases),
            LevelBreakdown = BuildBreakdown([
                new("L1", cases.Count(irCase => GetAssignedLevel(irCase) == "L1")),
                new("L2", cases.Count(irCase => GetAssignedLevel(irCase) == "L2")),
                new("Admin", cases.Count(irCase => GetAssignedLevel(irCase) == "Admin")),
                new("None", cases.Count(irCase => GetAssignedLevel(irCase) == "None"))
            ]),
            CreatedOverTime = BuildCreatedOverTime(cases, range)
        };
    }

    private static IReadOnlyList<AnalyticsBreakdownItem> BuildQueueBreakdown(IReadOnlyList<Case> cases)
    {
        var knownQueues = new[] { IncidentResponseQueue, ItSupportQueue, AdminReviewQueue };
        var queueCounts = cases
            .GroupBy(irCase => string.IsNullOrWhiteSpace(irCase.AssignedTeam) ? "Unspecified" : irCase.AssignedTeam)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var seeds = knownQueues
            .Select(queue => new AnalyticsBreakdownSeed(queue, queueCounts.GetValueOrDefault(queue)))
            .Concat(queueCounts
                .Where(item => !knownQueues.Contains(item.Key, StringComparer.Ordinal))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new AnalyticsBreakdownSeed(item.Key, item.Value)));

        return BuildBreakdown(seeds);
    }

    private static IReadOnlyList<AnalyticsBreakdownItem> BuildCreatedOverTime(IReadOnlyList<Case> cases, AnalyticsRange range)
    {
        if (range.GroupByHour)
        {
            var firstBucket = new DateTimeOffset(
                range.StartUtc.UtcDateTime.Year,
                range.StartUtc.UtcDateTime.Month,
                range.StartUtc.UtcDateTime.Day,
                range.StartUtc.UtcDateTime.Hour,
                0,
                0,
                TimeSpan.Zero);
            var buckets = new List<AnalyticsBreakdownSeed>();
            for (var bucketStart = firstBucket; bucketStart < range.EndExclusiveUtc; bucketStart = bucketStart.AddHours(1))
            {
                var bucketEnd = bucketStart.AddHours(1);
                buckets.Add(new AnalyticsBreakdownSeed(
                    bucketStart.ToLocalTime().ToString("MMM d HH:00", CultureInfo.InvariantCulture),
                    cases.Count(irCase => irCase.OpenedAt >= bucketStart && irCase.OpenedAt < bucketEnd)));
            }

            return BuildBreakdown(buckets);
        }

        var startDate = DateOnly.FromDateTime(range.StartUtc.UtcDateTime);
        var endDate = DateOnly.FromDateTime(range.EndExclusiveUtc.AddTicks(-1).UtcDateTime);
        var dayBuckets = new List<AnalyticsBreakdownSeed>();
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);
            dayBuckets.Add(new AnalyticsBreakdownSeed(
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                cases.Count(irCase => irCase.OpenedAt >= dayStart && irCase.OpenedAt < dayEnd)));
        }

        return BuildBreakdown(dayBuckets);
    }

    private static IReadOnlyList<AnalyticsBreakdownItem> BuildBreakdown(IEnumerable<AnalyticsBreakdownSeed> seeds)
    {
        var seedList = seeds.ToList();
        var maxCount = seedList.Count == 0 ? 0 : seedList.Max(item => item.Count);
        return seedList
            .Select(item => new AnalyticsBreakdownItem
            {
                Label = item.Label,
                Count = item.Count,
                Percent = maxCount == 0 ? 0 : (int)Math.Round(item.Count * 100.0 / maxCount)
            })
            .ToList();
    }

    private static string GetAssignedLevel(Case irCase)
    {
        return irCase.Assignments
            .OrderByDescending(assignment => assignment.AssignedAt)
            .Select(assignment => assignment.ApplicationUser?.Role?.Name)
            .FirstOrDefault(roleName => !string.IsNullOrWhiteSpace(roleName)) switch
        {
            RoleNames.AnalystLevel1 => "L1",
            RoleNames.AnalystLevel2 => "L2",
            RoleNames.Admin => "Admin",
            _ => "None"
        };
    }

    private async Task<IActionResult> UpdateCaseLifecycleAsync(
        string id,
        CaseStatus status,
        DateTimeOffset? closedAt,
        string action,
        string summary,
        Func<Case, bool> canUpdate,
        Func<Case, IReadOnlyList<string>>? validate = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!canUpdate(irCase))
        {
            return Forbid();
        }

        var missingFields = validate?.Invoke(irCase) ?? [];
        if (missingFields.Count > 0)
        {
            TempData["StatusMessage"] = BuildMissingFieldsMessage("Case was not closed", missingFields);
            return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;
        irCase.Status = status;
        irCase.ClosedAt = closedAt;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = userId,
            Action = action,
            EntityType = nameof(Case),
            EntityId = irCase.CaseId,
            Summary = summary
        });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"{summary.TrimEnd('.')} {irCase.CaseId}.";
        return RedirectToAction(nameof(Details), new { id = irCase.CaseId });
    }

    private async Task<IActionResult> CaseNotFoundOrForbiddenAsync(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) &&
            await db.Cases.AsNoTracking().AnyAsync(caseRecord => caseRecord.CaseId == id))
        {
            return Forbid();
        }

        return NotFound();
    }

    private CaseWorkspaceViewModel BuildWorkspaceViewModel(
        Case irCase,
        EvidenceMetadataViewModel? newEvidence = null,
        InvestigationDetailsViewModel? investigationDetails = null,
        TimelineEntryViewModel? newTimelineEntry = null)
    {
        var completedSteps = irCase.PlaybookSteps
            .Where(step => step.IsCompleted)
            .Select(step => step.StepKey)
            .ToHashSet(StringComparer.Ordinal);

        var playbookSteps = playbookDefinitionService
            .GetSteps(irCase.CaseType)
            .Select(step =>
            {
                var autoCompletionReason = GetAutoCompletionReason(irCase, step);
                var isAutoManaged = IsAutoManagedStep(step);
                return new PlaybookStepViewModel
                {
                    Key = step.Key,
                    Title = step.Title,
                    Description = step.Description,
                    IsAutoManaged = isAutoManaged,
                    IsManuallyCompleted = !isAutoManaged && completedSteps.Contains(step.Key),
                    IsAutoCompleted = autoCompletionReason is not null,
                    AutoCompletionReason = autoCompletionReason
                };
            })
            .ToList();

        return new CaseWorkspaceViewModel
        {
            Case = irCase,
            PlaybookSteps = playbookSteps,
            NewEvidence = newEvidence ?? new EvidenceMetadataViewModel(),
            InvestigationDetails = investigationDetails ?? InvestigationDetailsViewModel.FromCase(irCase),
            NewTimelineEntry = newTimelineEntry ?? new TimelineEntryViewModel { ActivityType = "Note" },
            CanWorkCase = caseAccessService.CanModifyEvidence(irCase, User) ||
                caseAccessService.CanModifyPlaybook(irCase, User),
            CanCloseCase = caseAccessService.CanCloseCase(irCase, User),
            CanReopenCase = caseAccessService.CanReopenCase(irCase, User),
            CanEscalateCase = caseAccessService.CanEscalateCase(irCase, User)
        };
    }

    private static bool IsAutoManagedStep(PlaybookStepDefinition step)
    {
        return step.AutoCompletionSignals != PlaybookAutoCompletionSignals.None;
    }

    private static string? GetAutoCompletionReason(Case irCase, PlaybookStepDefinition step)
    {
        var reasons = new List<string>();
        var signals = step.AutoCompletionSignals;

        if (signals.HasFlag(PlaybookAutoCompletionSignals.DetectionSource) &&
            irCase.DetectionSource is not null)
        {
            reasons.Add("detection source is documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.AlertReportedAt) &&
            irCase.AlertReportedAtUtc is not null)
        {
            reasons.Add("alert/report time is documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.AffectedUsers) &&
            !string.IsNullOrWhiteSpace(irCase.AffectedUsers))
        {
            reasons.Add("affected user details are documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.AffectedAssets) &&
            !string.IsNullOrWhiteSpace(irCase.AffectedAssets))
        {
            reasons.Add("affected asset details are documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.InvolvedAppsOrTools) &&
            !string.IsNullOrWhiteSpace(irCase.InvolvedAppsOrTools))
        {
            reasons.Add("involved apps or tools are documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.InitialFindings) &&
            !string.IsNullOrWhiteSpace(irCase.InitialFindings))
        {
            reasons.Add("initial findings are documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.Evidence) &&
            irCase.EvidenceItems.Count > 0)
        {
            reasons.Add("evidence metadata exists");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.IocSummary) &&
            !string.IsNullOrWhiteSpace(irCase.IocSummary))
        {
            reasons.Add("IOC summary is documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.ContainmentActions) &&
            !string.IsNullOrWhiteSpace(irCase.ContainmentActions))
        {
            reasons.Add("containment actions are documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.EscalationReason) &&
            !string.IsNullOrWhiteSpace(irCase.EscalationReason))
        {
            reasons.Add("escalation reason is documented");
        }

        if (signals.HasFlag(PlaybookAutoCompletionSignals.ClosureSummary) &&
            !string.IsNullOrWhiteSpace(irCase.ClosureSummary))
        {
            reasons.Add("closure summary is documented");
        }

        return reasons.Count == 0
            ? null
            : $"Auto-completed because {string.Join(" and ", reasons)}.";
    }
}
