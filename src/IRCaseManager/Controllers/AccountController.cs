using System.Security.Claims;
using IRCaseManager.Data;
using IRCaseManager.Models;
using IRCaseManager.Security;
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
    private const string GenericLoginFailureMessage = "Invalid username or password.";

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
        model.UserName = model.UserName?.Trim() ?? string.Empty;
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedUserName = model.UserName.Trim();
        model.UserName = normalizedUserName;

        var user = await db.Users
            .Include(applicationUser => applicationUser.Role)
            .SingleOrDefaultAsync(applicationUser => applicationUser.UserName == normalizedUserName);

        var now = DateTimeOffset.UtcNow;
        if (user?.LockoutEndUtc is not null && user.LockoutEndUtc <= now)
        {
            user.LockoutEndUtc = null;
            user.FailedLoginAttempts = 0;
        }

        if (user?.LockoutEndUtc is not null && user.LockoutEndUtc > now)
        {
            AddLoginAudit(user, normalizedUserName, "LoginLockedOut", "Local login rejected.");
            await db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, GenericLoginFailureMessage);
            return View(model);
        }

        var canVerifyPassword = user is not null && user.IsActive && user.Role is not null;
        var passwordVerification = canVerifyPassword
            ? new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user!, user!.PasswordHash, model.Password)
            : PasswordVerificationResult.Failed;

        if (passwordVerification == PasswordVerificationResult.Failed || !canVerifyPassword)
        {
            if (user is not null && user.IsActive && user.Role is not null)
            {
                user.FailedLoginAttempts += 1;
                user.LastFailedLoginUtc = now;

                var auditAction = "LoginFailed";
                var auditSummary = "Local login failed.";
                if (user.FailedLoginAttempts >= LoginLockoutPolicy.MaxFailedAttempts)
                {
                    user.LockoutEndUtc = now.Add(LoginLockoutPolicy.LockoutDuration);
                    auditAction = "LoginLockoutStarted";
                    auditSummary = "Local login failed and temporary account lockout started.";
                }

                AddLoginAudit(user, normalizedUserName, auditAction, auditSummary);
            }
            else
            {
                AddLoginAudit(null, normalizedUserName, "LoginFailed", "Local login failed.");
            }

            await db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, GenericLoginFailureMessage);
            return View(model);
        }

        user!.LastLoginAt = now;
        user.FailedLoginAttempts = 0;
        user.LastFailedLoginUtc = null;
        user.LockoutEndUtc = null;
        AddLoginAudit(user, normalizedUserName, "Login", "Local user login.");
        var roleName = user.Role!.Name;
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, roleName)
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

    private void AddLoginAudit(ApplicationUser? user, string userName, string action, string summary)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ApplicationUserId = user?.Id,
            Action = action,
            EntityType = nameof(ApplicationUser),
            EntityId = Truncate(user?.UserName ?? userName, 80),
            Summary = BuildLoginAuditSummary(summary)
        });
    }

    private string BuildLoginAuditSummary(string summary)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var details = summary;
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            details += $" RemoteIp={remoteIp}.";
        }

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            details += $" UserAgent={Truncate(userAgent, 180)}.";
        }

        return Truncate(details, 512);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
