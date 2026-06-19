using System.Security.Claims;
using IRCaseManager.Extensions;
using IRCaseManager.Models;
using IRCaseManager.Security;

namespace IRCaseManager.Services;

public class CaseAccessService
{
    private static readonly string[] AnalystVisibleValues =
    [
        "Default",
        "Analyst",
        "Analysts",
        "AnalystOnly",
        "All"
    ];

    public IQueryable<Case> FilterVisibleCases(IQueryable<Case> cases, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return cases.Where(_ => false);
        }

        if (user.IsInRole(RoleNames.Admin))
        {
            return cases;
        }

        if (user.IsInRole(RoleNames.AnalystLevel2))
        {
            return cases.Where(caseRecord =>
                caseRecord.CreatedById == userId.Value ||
                caseRecord.Assignments.Any(assignment => assignment.ApplicationUserId == userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility) ||
                caseRecord.Status == CaseStatus.Escalated);
        }

        if (user.IsInRole(RoleNames.AnalystLevel1))
        {
            return cases.Where(caseRecord =>
                caseRecord.CreatedById == userId.Value ||
                caseRecord.Assignments.Any(assignment => assignment.ApplicationUserId == userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility));
        }

        if (user.IsInRole(RoleNames.Auditor))
        {
            return cases.Where(caseRecord => caseRecord.Status == CaseStatus.Closed);
        }

        return cases.Where(_ => false);
    }
}
