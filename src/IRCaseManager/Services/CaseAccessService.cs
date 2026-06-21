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
                caseRecord.AssignmentHistory.Any(history =>
                    history.FromUserId == userId.Value ||
                    history.ToUserId == userId.Value ||
                    history.PerformedByUserId == userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility));
        }

        if (user.IsInRole(RoleNames.AnalystLevel1))
        {
            return cases.Where(caseRecord =>
                caseRecord.CreatedById == userId.Value ||
                caseRecord.Assignments.Any(assignment => assignment.ApplicationUserId == userId.Value) ||
                caseRecord.AssignmentHistory.Any(history =>
                    history.FromUserId == userId.Value ||
                    history.ToUserId == userId.Value ||
                    history.PerformedByUserId == userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility));
        }

        if (user.IsInRole(RoleNames.Auditor))
        {
            return cases.Where(caseRecord => caseRecord.Status == CaseStatus.Closed);
        }

        return cases.Where(_ => false);
    }

    public bool CanViewCase(Case caseRecord, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return false;
        }

        if (user.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        if (user.IsInRole(RoleNames.AnalystLevel2))
        {
            return caseRecord.CreatedById == userId.Value ||
                IsAssignedToUser(caseRecord, userId.Value) ||
                IsAssignmentHistoryParticipant(caseRecord, userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility);
        }

        if (user.IsInRole(RoleNames.AnalystLevel1))
        {
            return caseRecord.CreatedById == userId.Value ||
                IsAssignedToUser(caseRecord, userId.Value) ||
                IsAssignmentHistoryParticipant(caseRecord, userId.Value) ||
                AnalystVisibleValues.Contains(caseRecord.Visibility);
        }

        if (user.IsInRole(RoleNames.Auditor))
        {
            return caseRecord.Status == CaseStatus.Closed;
        }

        return false;
    }

    public bool CanEditCase(Case caseRecord, ClaimsPrincipal user)
    {
        if (user.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        return caseRecord.Status != CaseStatus.Closed && IsAnalystAuthorizedToWorkCase(caseRecord, user);
    }

    public bool CanCloseCase(Case caseRecord, ClaimsPrincipal user)
    {
        if (caseRecord.Status == CaseStatus.Closed)
        {
            return false;
        }

        return user.IsInRole(RoleNames.Admin) || IsAnalystAuthorizedToWorkCase(caseRecord, user);
    }

    public bool CanReopenCase(Case caseRecord, ClaimsPrincipal user)
    {
        if (caseRecord.Status != CaseStatus.Closed)
        {
            return false;
        }

        return user.IsInRole(RoleNames.Admin);
    }

    public bool CanEscalateCase(Case caseRecord, ClaimsPrincipal user)
    {
        if (caseRecord.Status == CaseStatus.Closed || user.IsInRole(RoleNames.Admin))
        {
            return false;
        }

        if (user.IsInRole(RoleNames.AnalystLevel1))
        {
            return caseRecord.Status != CaseStatus.Escalated && IsAnalystAuthorizedToWorkCase(caseRecord, user);
        }

        return user.IsInRole(RoleNames.AnalystLevel2) && IsAnalystAuthorizedToWorkCase(caseRecord, user);
    }

    public bool CanModifyEvidence(Case caseRecord, ClaimsPrincipal user)
    {
        return CanModifyCaseWork(caseRecord, user);
    }

    public bool CanModifyPlaybook(Case caseRecord, ClaimsPrincipal user)
    {
        return CanModifyCaseWork(caseRecord, user);
    }

    private static bool CanModifyCaseWork(Case caseRecord, ClaimsPrincipal user)
    {
        if (caseRecord.Status == CaseStatus.Closed)
        {
            return false;
        }

        return user.IsInRole(RoleNames.Admin) || IsAnalystAuthorizedToWorkCase(caseRecord, user);
    }

    private static bool IsAnalystAuthorizedToWorkCase(Case caseRecord, ClaimsPrincipal user)
    {
        if (!user.IsInRole(RoleNames.AnalystLevel1) && !user.IsInRole(RoleNames.AnalystLevel2))
        {
            return false;
        }

        var userId = user.GetUserId();
        if (userId is null)
        {
            return false;
        }

        if (IsAssignedToUser(caseRecord, userId.Value))
        {
            return true;
        }

        return caseRecord.Status != CaseStatus.Escalated &&
            (caseRecord.CreatedById == userId.Value || IsUnassignedAnalystVisibleCase(caseRecord));
    }

    private static bool IsAssignmentHistoryParticipant(Case caseRecord, int userId)
    {
        return caseRecord.AssignmentHistory.Any(history =>
            history.FromUserId == userId ||
            history.ToUserId == userId ||
            history.PerformedByUserId == userId);
    }

    private static bool IsAssignedToUser(Case caseRecord, int userId)
    {
        return caseRecord.Assignments.Any(assignment => assignment.ApplicationUserId == userId);
    }

    private static bool IsUnassignedAnalystVisibleCase(Case caseRecord)
    {
        return !caseRecord.Assignments.Any() && AnalystVisibleValues.Contains(caseRecord.Visibility);
    }
}
