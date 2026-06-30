using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SupportTicketSystem.Models;
using SupportTicketSystem.data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using SupportTicketSystem.Utils;

namespace SupportTicketSystem.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;
    private readonly AccountLockoutOptions _lockout;

    public AuthController(AppDbContext db, ILogger<AuthController> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _lockout = config.GetSection("Security:Lockout").Get<AccountLockoutOptions>() ?? new AccountLockoutOptions();
    }

    //GET /auth
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    //POST /auth
    [HttpPost]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Index(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", model);

        UserModel? user = Authenticator.FindUser(_db, model.Email);

        // Account-level lockout (known email only). Unknown emails are throttled by the
        // per-IP rate limiter and fall through to the timing-equal dummy-hash check below.
        if (user is { IsActive: false })
        {
            _logger.LogWarning("Disabled account {UserId} login attempted", user.Id);
            ModelState.AddModelError(string.Empty, "This account has been disabled.");
            return View("Index", model);
        }

        if (user is { LockoutEnd: not null } && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Locked account {UserId} login attempted", user.Id);
            ModelState.AddModelError(string.Empty, "This account is temporarily locked. Try again later.");
            return View("Index", model);
        }

        if (Authenticator.AuthenticateUser(user, model.Password))
        {
            if (user != null)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await _db.SaveChangesAsync();
            }
            await Authenticator.SignIn(HttpContext, user!);
            _logger.LogInformation("User {UserId} signed in", user!.Id);
            return RedirectToAction("Index", "Tickets");
        }

        // Failed. Increment counter for known accounts and lock when threshold is reached.
        if (user != null)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= _lockout.MaxFailedAttempts)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.Add(_lockout.LockoutDuration);
                _logger.LogWarning("User {UserId} locked after {Attempts} failed attempts", user.Id, user.FailedLoginAttempts);
            }
            await _db.SaveChangesAsync();
        }

        // user may be null here (unknown email); log a stable hash, not the raw email.
        _logger.LogWarning("Failed login attempt for {EmailHash}", PiiHash.Email(model.Email));
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        return View("Index", model);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User {UserId} signed out", userId);
        return RedirectToAction("Index", "Home");
    }
}
