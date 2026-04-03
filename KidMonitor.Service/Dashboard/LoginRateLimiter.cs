using System.Collections.Concurrent;

namespace KidMonitor.Service.Dashboard;

/// <summary>
/// In-memory login rate limiter. Tracks failed attempts per client IP and enforces
/// a 15-minute lockout after 5 consecutive failures within a 5-minute window.
/// </summary>
public sealed class LoginRateLimiter
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class Attempts
    {
        public int Count;
        public DateTime WindowStart = DateTime.UtcNow;
        public DateTime? LockedUntil;
    }

    private readonly ConcurrentDictionary<string, Attempts> _state = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns true if the caller is currently locked out.</summary>
    public bool IsLockedOut(string clientIp)
    {
        if (!_state.TryGetValue(clientIp, out var entry))
            return false;

        if (entry.LockedUntil.HasValue && DateTime.UtcNow < entry.LockedUntil.Value)
            return true;

        return false;
    }

    /// <summary>Records a failed login attempt. Returns true if this attempt triggered a lockout.</summary>
    public bool RecordFailure(string clientIp)
    {
        var entry = _state.GetOrAdd(clientIp, _ => new Attempts());

        lock (entry)
        {
            // Reset window if it has expired
            if (DateTime.UtcNow - entry.WindowStart > FailureWindow)
            {
                entry.Count = 0;
                entry.WindowStart = DateTime.UtcNow;
                entry.LockedUntil = null;
            }

            entry.Count++;

            if (entry.Count >= MaxFailures)
            {
                entry.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                return true;
            }
        }

        return false;
    }

    /// <summary>Clears the failure record for a client IP after a successful login.</summary>
    public void RecordSuccess(string clientIp) =>
        _state.TryRemove(clientIp, out _);

    /// <summary>Returns when the lockout expires, or null if not locked out.</summary>
    public DateTime? GetLockoutExpiry(string clientIp)
    {
        if (!_state.TryGetValue(clientIp, out var entry))
            return null;

        return entry.LockedUntil.HasValue && DateTime.UtcNow < entry.LockedUntil.Value
            ? entry.LockedUntil
            : null;
    }
}
