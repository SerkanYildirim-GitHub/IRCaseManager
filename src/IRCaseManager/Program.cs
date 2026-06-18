using IRCaseManager.Data;
using IRCaseManager.Security;
using IRCaseManager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<CaseIdGenerator>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.CanManageUsers, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy(AuthorizationPolicies.CanDeleteCases, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy(AuthorizationPolicies.CanReopenCases, policy => policy.RequireRole(RoleNames.Admin, RoleNames.AnalystLevel2));
    options.AddPolicy(AuthorizationPolicies.CanCreateCases, policy => policy.RequireRole(RoleNames.Admin, RoleNames.AnalystLevel2, RoleNames.AnalystLevel1));
    options.AddPolicy(AuthorizationPolicies.CanEditCases, policy => policy.RequireRole(RoleNames.Admin, RoleNames.AnalystLevel2, RoleNames.AnalystLevel1));
    options.AddPolicy(AuthorizationPolicies.ReadOnlyAccess, policy => policy.RequireRole(RoleNames.All));
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    await SeedData.InitializeDevelopmentAsync(app.Services, app.Configuration);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
