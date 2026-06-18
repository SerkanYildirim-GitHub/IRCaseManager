namespace IRCaseManager.Security;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string AnalystLevel2 = "Analyst Level 2";
    public const string AnalystLevel1 = "Analyst Level 1";
    public const string Auditor = "Auditor";

    public static readonly string[] All =
    [
        Admin,
        AnalystLevel2,
        AnalystLevel1,
        Auditor
    ];
}
