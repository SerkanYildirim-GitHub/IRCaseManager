using IRCaseManager.Data;
using IRCaseManager.Models;
using IRCaseManager.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var cases = db.Cases.AsNoTracking();

        ViewBag.TotalCases = await cases.CountAsync();
        ViewBag.CriticalCases = await cases.CountAsync(irCase => irCase.Severity == CaseSeverity.Critical && irCase.Status != CaseStatus.Closed);
        ViewBag.WaitingCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Waiting);
        ViewBag.ClosedCases = await cases.CountAsync(irCase => irCase.Status == CaseStatus.Closed);

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
