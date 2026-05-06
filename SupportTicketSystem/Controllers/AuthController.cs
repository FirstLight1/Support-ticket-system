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
using Microsoft.AspNetCore.Http;

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
    public static UserModel FindUser(AppDbContext db, string email)
    {
        try
        {
            return db.Users.FirstOrDefault(u => u.Email == email);
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    /// <summary>
    /// Zisti ci pre zadanie email sa zhoduuje hash hesla
    /// </summary>
    /// <param name="db"></param>
    /// <param name="user"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static bool AuthenticateUser(UserModel user, string password)
    {
        if (user == null) return false;
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Success)
        {
            return true;
        }
        return false;
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

    public AuthController(AppDbContext db)
    {
        _db = db;
    }
    
    //GET /auth
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
    
    //POST /auth 
    [HttpPost]
    public async Task<IActionResult> Index(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View("Index",model);

        UserModel user = Authenticator.FindUser(_db, model.Email);
        
        if (Authenticator.AuthenticateUser(user, model.Password))
        {
            await Authenticator.SignIn(HttpContext, user);
            return RedirectToAction("Index", "Tickets");
        }
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
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}