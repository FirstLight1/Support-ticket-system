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
using Microsoft.Extensions.Logging;
using Serilog;
using SupportTicketSystem.Utils;

namespace SupportTicketSystem.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, ILogger<AuthController> logger)
    {
        _db = db;
        _logger = logger;
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
        if (!ModelState.IsValid) return View("Index",model);

        UserModel? user = Authenticator.FindUser(_db, model.Email);

        if (Authenticator.AuthenticateUser(user, model.Password))
        {
            await Authenticator.SignIn(HttpContext, user);
            _logger.LogInformation("User {Email} signed in", user.Email);
            return RedirectToAction("Index", "Tickets");
        }
        // user may be null here (unknown email), so log the submitted email, not user.Email.
        _logger.LogWarning("Failed login attempt for {Email}", model.Email);
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        return View("Index", model);
    }

    //Untested
    /// <summary>
    /// Zrusi Authcokkie pre pouzivatela
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> Logout()
    {
        var email = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User {Email} signed out", email);
        return RedirectToAction("Index", "Home");
    }
}
