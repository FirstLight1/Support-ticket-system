using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SupportTicketSystem.Models;
using SupportTicketSystem.data;
using System.Security.Cryptography;
using System.Text;

namespace SupportTicketSystem.Controllers;

public static class Authenticator
{
    
    private static string HashPassword(string Password)
    {
        var sha256Hash = SHA256.Create();
        byte[] passwordHash = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(Password));
        StringBuilder builder = new StringBuilder();
        foreach (byte b in passwordHash)
        {
            builder.Append(b.ToString("x2")); // Convert to hexadecimal string
        }
        
        return builder.ToString();
    }

    private static User FindUser(AppDbContext db, string email)
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

    public static bool AuthenticateUser(AppDbContext db, string email, string password)
    {
        var user = FindUser(db, email);
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

        if (Authenticator.AuthenticateUser(_db, model.Email, model.Password))
        {
            return View("../Tickets/Index");
        }
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        return View("Index", model);
    }
}