using IRCaseManager.Data;
using IRCaseManager.Extensions;
using IRCaseManager.Models;
using IRCaseManager.Security;
using IRCaseManager.Services;
using IRCaseManager.ViewModels;
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
    public async Task<IActionResult> Index()
    {
        var visibleCaseSet = await caseAccessService
            .FilterVisibleCases(db.Cases.AsNoTracking(), User)
            .Include(irCase => irCase.CreatedBy)
            .Include(irCase => irCase.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
            .ToListAsync();

        var cases = visibleCaseSet
            .OrderByDescending(irCase => irCase.OpenedAt)
            .ToList();

        return View(cases);
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
            .Include(caseRecord => caseRecord.EvidenceItems)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .Include(caseRecord => caseRecord.TimelineEntries)
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

        if (model.AssignedUserId is not null)
        {
            if (!await IsAssignableAnalystAsync(model.AssignedUserId.Value))
            {
                ModelState.AddModelError(nameof(model.AssignedUserId), "Select a valid Analyst Level 1 or Analyst Level 2 user.");
                await PopulateCreateCaseOptionsAsync();
                return View(model);
            }
        }

        var openedAt = DateTimeOffset.UtcNow;
        var caseId = await caseIdGenerator.GenerateAsync(openedAt);
        var sourceReference = GenerateSourceReference(caseId);
        var caseTypeName = model.CaseType!.Value.GetDisplayName();
        var irCase = new Case
        {
            CaseId = caseId,
            Title = $"{caseId} - {caseTypeName}",
            Severity = model.Severity!.Value,
            CaseType = model.CaseType!.Value,
            Status = model.AssignedUserId is null ? CaseStatus.New : CaseStatus.Assigned,
            SourceReference = sourceReference,
            AssignedTeam = model.AssignedTeam,
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

        if (!CanEditCase(irCase))
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
            AssignedUserId = irCase.Assignments.Select(assignment => (int?)assignment.ApplicationUserId).FirstOrDefault(),
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

        if (!CanEditCase(irCase))
        {
            return Forbid();
        }

        if (model.AssignedUserId is not null && !await IsAssignableAnalystAsync(model.AssignedUserId.Value))
        {
            ModelState.AddModelError(nameof(model.AssignedUserId), "Select a valid Analyst Level 1 or Analyst Level 2 user.");
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
        var caseTypeName = model.CaseType!.Value.GetDisplayName();
        irCase.CaseType = model.CaseType.Value;
        irCase.Title = $"{irCase.CaseId} - {caseTypeName}";
        irCase.Severity = model.Severity!.Value;
        irCase.AssignedTeam = model.AssignedTeam;
        irCase.InitialSummary = model.InitialSummary;
        irCase.UpdatedAt = now;
        irCase.UpdatedById = userId.Value;

        var selectedAssignedUserId = model.AssignedUserId;
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

    [HttpPost]
    public async Task<IActionResult> Close(string id)
    {
        if (!CanCloseCase())
        {
            return Forbid();
        }

        var updateResult = await UpdateCaseLifecycleAsync(
            id,
            CaseStatus.Closed,
            closedAt: DateTimeOffset.UtcNow,
            action: "CaseClosed",
            summary: "Case closed.");

        return updateResult;
    }

    [HttpPost]
    public async Task<IActionResult> Reopen(string id)
    {
        if (!CanReopenCase())
        {
            return Forbid();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
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

    [HttpPost]
    public async Task<IActionResult> Escalate(string id)
    {
        if (!CanEscalateCase())
        {
            return Forbid();
        }

        var updateResult = await UpdateCaseLifecycleAsync(
            id,
            CaseStatus.Escalated,
            closedAt: null,
            action: "CaseEscalated",
            summary: "Case escalated.");

        return updateResult;
    }

    [HttpPost]
    public async Task<IActionResult> TogglePlaybookStep(string id, string stepKey, string? completionState)
    {
        if (!CanWorkCase())
        {
            return Forbid();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        var definition = playbookDefinitionService
            .GetSteps(irCase.CaseType)
            .SingleOrDefault(step => step.Key == stepKey);

        if (definition is null)
        {
            return NotFound();
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

    [HttpPost]
    public async Task<IActionResult> AddEvidence(string id, EvidenceMetadataViewModel model)
    {
        if (!CanWorkCase())
        {
            return Forbid();
        }

        model.Trim();

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
        }

        if (!ModelState.IsValid)
        {
            var reloadedCase = await caseAccessService
                .FilterVisibleCases(db.Cases.AsNoTracking(), User)
                .Include(caseRecord => caseRecord.CreatedBy)
                .Include(caseRecord => caseRecord.Assignments)
                    .ThenInclude(assignment => assignment.ApplicationUser)
                .Include(caseRecord => caseRecord.EvidenceItems)
                .Include(caseRecord => caseRecord.PlaybookSteps)
                .Include(caseRecord => caseRecord.TimelineEntries)
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
            new() { Value = "Incident Response", Text = "Incident Response" },
            new() { Value = "IT Support", Text = "IT Support" }
        };

        var users = await db.Users
            .AsNoTracking()
            .Include(applicationUser => applicationUser.Role)
            .Where(applicationUser =>
                applicationUser.IsActive &&
                applicationUser.Role != null &&
                (applicationUser.Role.Name == RoleNames.AnalystLevel1 ||
                 applicationUser.Role.Name == RoleNames.AnalystLevel2))
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
                Text = GetAssignedUserOptionText(applicationUser.UserName, applicationUser.RoleName)
            })
            .ToList();
    }

    private async Task<bool> IsAssignableAnalystAsync(int userId)
    {
        return await db.Users
            .Include(applicationUser => applicationUser.Role)
            .AnyAsync(applicationUser =>
                applicationUser.Id == userId &&
                applicationUser.IsActive &&
                applicationUser.Role != null &&
                (applicationUser.Role.Name == RoleNames.AnalystLevel1 ||
                 applicationUser.Role.Name == RoleNames.AnalystLevel2));
    }

    private static string GetAssignedUserOptionText(string userName, string roleName)
    {
        return userName switch
        {
            "analyst.l1.local" => RoleNames.AnalystLevel1,
            "analyst.l2.local" => RoleNames.AnalystLevel2,
            _ => $"{userName} ({roleName})"
        };
    }

    private static string GenerateSourceReference(string caseId)
    {
        var sequence = caseId.Split('-').LastOrDefault();
        return int.TryParse(sequence, out var number) ? $"TK-{number:00000}" : "TK-00000";
    }

    private async Task<IActionResult> UpdateCaseLifecycleAsync(
        string id,
        CaseStatus status,
        DateTimeOffset? closedAt,
        string action,
        string summary)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var irCase = await caseAccessService
            .FilterVisibleCases(db.Cases, User)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return await CaseNotFoundOrForbiddenAsync(id);
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

    private bool CanCloseCase()
    {
        return User.IsInRole(RoleNames.Admin)
            || User.IsInRole(RoleNames.AnalystLevel1)
            || User.IsInRole(RoleNames.AnalystLevel2);
    }

    private bool CanReopenCase()
    {
        return User.IsInRole(RoleNames.Admin)
            || User.IsInRole(RoleNames.AnalystLevel2);
    }

    private bool CanEscalateCase()
    {
        return User.IsInRole(RoleNames.Admin)
            || User.IsInRole(RoleNames.AnalystLevel1)
            || User.IsInRole(RoleNames.AnalystLevel2);
    }

    private bool CanWorkCase()
    {
        return User.IsInRole(RoleNames.Admin)
            || User.IsInRole(RoleNames.AnalystLevel1)
            || User.IsInRole(RoleNames.AnalystLevel2);
    }

    private bool CanEditCase(Case irCase)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        return irCase.Status != CaseStatus.Closed &&
            (User.IsInRole(RoleNames.AnalystLevel1) || User.IsInRole(RoleNames.AnalystLevel2));
    }

    private CaseWorkspaceViewModel BuildWorkspaceViewModel(
        Case irCase,
        EvidenceMetadataViewModel? newEvidence = null)
    {
        var completedSteps = irCase.PlaybookSteps
            .Where(step => step.IsCompleted)
            .Select(step => step.StepKey)
            .ToHashSet(StringComparer.Ordinal);

        var playbookSteps = playbookDefinitionService
            .GetSteps(irCase.CaseType)
            .Select(step => new PlaybookStepViewModel
            {
                Key = step.Key,
                Title = step.Title,
                Description = step.Description,
                IsCompleted = completedSteps.Contains(step.Key)
            })
            .ToList();

        return new CaseWorkspaceViewModel
        {
            Case = irCase,
            PlaybookSteps = playbookSteps,
            NewEvidence = newEvidence ?? new EvidenceMetadataViewModel(),
            CanWorkCase = CanWorkCase()
        };
    }
}
