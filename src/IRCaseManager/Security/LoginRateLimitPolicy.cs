namespace IRCaseManager.Security;

public static class LoginRateLimitPolicy
{
    public const int PermitLimit = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
