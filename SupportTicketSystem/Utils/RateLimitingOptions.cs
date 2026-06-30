using System;

namespace SupportTicketSystem.Utils;

public class RateLimitingOptions
{
    public RateLimitPolicyOptions Login { get; set; } = new();
    public RateLimitPolicyOptions Register { get; set; } = new();
    public RateLimitPolicyOptions Mutation { get; set; } = new();
}

public class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Account lockout thresholds. After <see cref="MaxFailedAttempts"/> failed logins for a
/// single account, the account is locked for <see cref="LockoutDuration"/>. Sourced from
/// the <c>Security:Lockout</c> config section.
/// </summary>
public class AccountLockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
