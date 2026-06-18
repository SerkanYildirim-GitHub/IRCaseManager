namespace IRCaseManager.Security;

public static class AuthorizationPolicies
{
    public const string CanManageUsers = "CanManageUsers";
    public const string CanCreateCases = "CanCreateCases";
    public const string CanEditCases = "CanEditCases";
    public const string CanReopenCases = "CanReopenCases";
    public const string CanDeleteCases = "CanDeleteCases";
    public const string ReadOnlyAccess = "ReadOnlyAccess";
}
