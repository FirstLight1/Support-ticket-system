using System;
using System.Security.Cryptography;
using System.Text;

namespace SupportTicketSystem.Utils;

/// <summary>
/// Stable, non-reversible identifier for PII (email addresses) written to logs.
/// Lets us correlate events per user without storing the raw email in plain-text
/// log streams. Not a security primitive — given the (small) input space of
/// email addresses this is brute-forceable; it only reduces casual exposure.
/// </summary>
public static class PiiHash
{
    private const string Salt = "SupportTicketSystem.LogPii.v1";

    public static string Email(string? email)
    {
        if (string.IsNullOrEmpty(email)) return "unknown";
        var bytes = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant() + Salt);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
