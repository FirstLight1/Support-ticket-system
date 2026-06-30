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
