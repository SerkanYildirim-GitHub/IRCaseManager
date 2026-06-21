using System.Collections.Concurrent;
using System.Net;
using IRCaseManager.Security;

namespace IRCaseManager.Services;

public class LoginRateLimiter(ILogger<LoginRateLimiter> logger)
{
    private readonly ConcurrentDictionary<string, LoginRateLimitWindow> windows = new(StringComparer.Ordinal);

    public bool IsAllowed(HttpContext httpContext, DateTimeOffset now)
    {
        var clientKey = GetClientKey(httpContext);
        var window = windows.GetOrAdd(clientKey, _ => new LoginRateLimitWindow());

        lock (window)
        {
            while (window.Requests.Count > 0 && now - window.Requests.Peek() >= LoginRateLimitPolicy.Window)
            {
                window.Requests.Dequeue();
            }

            if (window.Requests.Count >= LoginRateLimitPolicy.PermitLimit)
            {
                logger.LogDebug(
                    "Login rate limit rejected request for client key {ClientKey}. CurrentCount={CurrentCount}, Limit={PermitLimit}, WindowSeconds={WindowSeconds}.",
                    clientKey,
                    window.Requests.Count,
                    LoginRateLimitPolicy.PermitLimit,
                    LoginRateLimitPolicy.Window.TotalSeconds);
                return false;
            }

            window.Requests.Enqueue(now);
            logger.LogDebug(
                "Login rate limit allowed request for client key {ClientKey}. CurrentCount={CurrentCount}, Limit={PermitLimit}, WindowSeconds={WindowSeconds}.",
                clientKey,
                window.Requests.Count,
                LoginRateLimitPolicy.PermitLimit,
                LoginRateLimitPolicy.Window.TotalSeconds);
            return true;
        }
    }

    private static string GetClientKey(HttpContext httpContext)
    {
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return "unknown-client";
        }

        if (remoteIpAddress.IsIPv4MappedToIPv6)
        {
            remoteIpAddress = remoteIpAddress.MapToIPv4();
        }

        return IPAddress.IsLoopback(remoteIpAddress) ? "loopback" : remoteIpAddress.ToString();
    }

    private sealed class LoginRateLimitWindow
    {
        public Queue<DateTimeOffset> Requests { get; } = new();
    }
}
