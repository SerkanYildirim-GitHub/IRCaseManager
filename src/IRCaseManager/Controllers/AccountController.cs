using System.Security.Claims;
using IRCaseManager.Data;
using IRCaseManager.Models;
using IRCaseManager.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRCaseManager.Controllers;

public class AccountController(AppDbContext db) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        model.UserName = model.UserName.Trim();
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await db.Users
            .Include(applicationUser => applicationUser.Role)
            .SingleOrDefaultAsync(applicationUser => applicationUser.UserName == model.UserName && applicationUser.IsActive);

        var verified = user is not null &&
            new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash, model.Password) != PasswordVerificationResult.Failed;

        if (!verified || user?.Role is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = user.Id,
            Action = "Login",
            EntityType = nameof(ApplicationUser),
            EntityId = user.UserName,
            Summary = "Local user login."
        });
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.Name)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            });

        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Dashboard")!);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public IActionResult Profile()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
