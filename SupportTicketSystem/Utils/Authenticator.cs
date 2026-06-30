using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Serilog;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.Utils;

/// <summary>
/// Staticka classa na Authentikaciu pouzivatela.
/// Pouziteie Authenticator.FindUser(AppDbContext db, string email) alebo Authenticator.AuthenticateUser(UserModel? user, string password)
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
