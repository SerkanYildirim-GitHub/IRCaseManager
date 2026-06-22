using IRCaseManager.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction(nameof(CasesController.Analytics), "Cases");
    }
}
