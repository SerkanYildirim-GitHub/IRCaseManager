using IRCaseManager.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRCaseManager.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReadOnlyAccess)]
public class ResourcesController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
