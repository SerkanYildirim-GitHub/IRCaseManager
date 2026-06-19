using IRCaseManager.Data;
using IRCaseManager.Models;
using IRCaseManager.Security;
using IRCaseManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class DashboardController(AppDbContext db, CaseAccessService caseAccessService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var cases = caseAccessService.FilterVisibleCases(db.Cases.AsNoTracking(), User);

        ViewBag.TotalCases = await cases.CountAsync();
        ViewBag.NewCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.New);
        ViewBag.AssignedCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Assigned);
        ViewBag.EscalatedCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Escalated);
        ViewBag.WaitingCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Waiting);
        ViewBag.ClosedCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Closed);
        ViewBag.CriticalCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.Critical);
        ViewBag.HighCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.High);
        ViewBag.MediumCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.Medium);
        ViewBag.LowCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.Low);
        ViewBag.InformationalCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.Informational);

        var visibleCaseSet = await cases
            .OrderByDescending(irCase => irCase.Id)
            .Take(50)
            .ToListAsync();

        var recentCases = visibleCaseSet
            .OrderByDescending(irCase => irCase.OpenedAt)
            .Take(8)
            .ToList();

        return View(recentCases);
    }
}
