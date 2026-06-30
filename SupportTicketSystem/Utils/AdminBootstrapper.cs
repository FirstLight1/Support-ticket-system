using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Serilog;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.Utils;

/// <summary>
/// Idempotent startup admin provisioning. When <c>BootstrapAdmin:Email</c> and
/// <c>BootstrapAdmin:Password</c> are configured (e.g. via the
/// <c>BootstrapAdmin__Email</c> / <c>BootstrapAdmin__Password</c> environment
/// variables) and no user with that email exists yet, one admin account is
/// created. Intended for bootstrapping the first production admin without
/// hardcoded credentials. Subsequent boots are a no-op. Safe when unconfigured.
/// </summary>
public static class AdminBootstrapper
{
    public static void EnsureAdmin(AppDbContext db, IConfiguration config)
    {
        var email = config["BootstrapAdmin:Email"];
        var password = config["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var existing = db.Users.FirstOrDefault(u => u.Email == email);
        if (existing != null)
        {
            if (!existing.IsAdmin)
                Log.Warning("BootstrapAdmin {Email} exists but is not an admin; role left unchanged", email);
            return;
        }

        var admin = new UserModel
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = string.Empty,
            IsAdmin = true
        };
        admin.PasswordHash = Authenticator.HashPassword(admin, password);

        db.Users.Add(admin);
        db.SaveChanges();
        Log.Information("Bootstrapped admin account from configuration");
    }
}
