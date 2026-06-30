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

namespace SupportTicketSystem.Controllers;

/// <summary>
/// Staticka classa na Authentikaciu pouzivatela.
/// Pouziteie Authenticator.FindUser(AppDbContext db, string email) alebo Authenticator.AuthneticateUser(AppDbContext db, User user, string password)
/// </summary>
public static class Authenticator
{
    private static readonly PasswordHasher<UserModel> _hasher = new();


    public static string HashPassword(UserModel user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    /// <summary>
    /// Finds user in database
    /// </summary>
    /// <param name="db">databaza</param>
    /// <param name="email">email</param>
    /// <returns>Usera ak existuje inak null</returns>
    public static UserModel? FindUser(AppDbContext db, string email)
    {
        try
        {
            return db.Users.FirstOrDefault(u => u.Email == email);
        }
        catch(Exception e)
        {
            Log.Error(e, "Failed to look up user by email {Email}", email);
            return null;
        }
    }

    private static readonly string _dummyHash =
        _hasher.HashPassword(new UserModel{Email = "", PasswordHash = ""}, "Hccztdg8cacC9tJ");


    /// <summary>
    /// Zisti ci pre zadanie email sa zhoduuje hash hesla
    /// </summary>
    /// <param name="db"></param>
    /// <param name="user"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static bool AuthenticateUser(UserModel? user, string password)
    {
        var hash = user?.PasswordHash ?? _dummyHash;
        var subject = user ?? new UserModel{Email =  "", PasswordHash = ""};

        var result = _hasher.VerifyHashedPassword(subject, hash, password);

        return user != null && result == PasswordVerificationResult.Success;
    }

    public static async Task SignIn(HttpContext httpContext, UserModel user)
    {
        // A "claim" is just a key/value pair asserting something about the user.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        //Toto realne vytvori session cookie
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            principal, new AuthenticationProperties
            {
                IsPersistent = true,        // survives browser close
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }

}

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
