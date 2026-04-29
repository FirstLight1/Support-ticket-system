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

namespace SupportTicketSystem.Controllers;

/// <summary>
/// Staticka classa na Authentikaciu pouzivatela.
/// Pouziteie Authenticator.FindUser(AppDbContext db, string email) alebo Authenticator.AuthneticateUser(AppDbContext db, User user, string password)
/// </summary>
public static class Authenticator
{
    /// <summary>
    /// Metoda, ktora vytvori SHA256 hash a nasledne ho convertuje do hexadecimalneho stringu
    /// </summary>
    /// <param name="password">User password</param>
    /// <returns>Hexadecimal string of users password</returns>
    private static string HashPassword(string password)
    {
        var sha256Hash = SHA256.Create();
        byte[] passwordHash = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
        StringBuilder builder = new StringBuilder();
        foreach (byte b in passwordHash)
        {
            builder.Append(b.ToString("x2")); // Convert to hexadecimal string
        }
        
        return builder.ToString();
    }

    /// <summary>
    /// Finds user in database
    /// </summary>
    /// <param name="db">databaza</param>
    /// <param name="email">email</param>
    /// <returns>Usera ak existuje inak null</returns>
    public static User FindUser(AppDbContext db, string email)
    {
        try
        {
            return db.Users.FirstOrDefault(u => u.Email == email);
        }
        catch
        {
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
    public static bool AuthenticateUser(AppDbContext db, User user, string password)
    {
        string hashedPassword = HashPassword(password);
        if (user != null)
        {
            return hashedPassword.Equals(user.PasswordHash);
        }
        return false;
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

        User user = Authenticator.FindUser(_db, model.Email);
        
        if (Authenticator.AuthenticateUser(_db, user, model.Password))
        {
            // A "claim" is just a key/value pair asserting something about the user. 
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, model.Email),
                new(ClaimTypes.Role, user.IsAdmin.ToString()),
            };
            
            var identity = new ClaimsIdentity(claims, "Token");
            var principal = new ClaimsPrincipal(identity);
            
            
            //Toto realne vytvori session cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,        // survives browser close
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
                });
            
            return View("../Tickets/Index");
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
        return View("../Home/Index");
    }
}