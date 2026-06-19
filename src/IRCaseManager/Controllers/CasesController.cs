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
    PlaybookDefinitionService playbookDefinitionService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var visibleCaseSet = await db.Cases
            .AsNoTracking()
            .Include(irCase => irCase.CreatedBy)
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

        var irCase = await db.Cases
            .AsNoTracking()
            .Include(caseRecord => caseRecord.CreatedBy)
            .Include(caseRecord => caseRecord.Assignments)
                .ThenInclude(assignment => assignment.ApplicationUser)
            .Include(caseRecord => caseRecord.EvidenceItems)
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .Include(caseRecord => caseRecord.TimelineEntries)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return NotFound();
        }

        return View("DetailsWorkspace", BuildWorkspaceViewModel(irCase));
    }

    [Authorize(Policy = AuthorizationPolicies.CanCreateCases)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateAssignedUserOptionsAsync();
        return View(new CreateCaseViewModel());
    }

    [Authorize(Policy = AuthorizationPolicies.CanCreateCases)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCaseViewModel model)
    {
        model.Trim();

        if (!ModelState.IsValid)
        {
            await PopulateAssignedUserOptionsAsync();
            return View(model);
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (model.AssignedUserId is not null)
        {
            var assignedUserExists = await db.Users.AnyAsync(applicationUser =>
                applicationUser.Id == model.AssignedUserId.Value && applicationUser.IsActive);

            if (!assignedUserExists)
            {
                ModelState.AddModelError(nameof(model.AssignedUserId), "Select a valid active user.");
                await PopulateAssignedUserOptionsAsync();
                return View(model);
            }
        }

        var openedAt = DateTimeOffset.UtcNow;
        var irCase = new Case
        {
            CaseId = await caseIdGenerator.GenerateAsync(openedAt),
            Title = model.Title,
            Severity = model.Severity!.Value,
            CaseType = model.CaseType!.Value,
            Status = CaseStatus.New,
            SourceReference = model.SourceReference,
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

        var irCase = await db.Cases
            .Include(caseRecord => caseRecord.Assignments)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return NotFound();
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

        var irCase = await db.Cases
            .Include(caseRecord => caseRecord.PlaybookSteps)
            .SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);

        if (irCase is null)
        {
            return NotFound();
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

        var irCase = await db.Cases.SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var reloadedCase = await db.Cases
                .AsNoTracking()
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

        var irCase = await db.Cases.SingleOrDefaultAsync(caseRecord => caseRecord.CaseId == id);
        if (irCase is null)
        {
            return NotFound();
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
