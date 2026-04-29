using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {

        if (!ModelState.IsValid) return View("Register",model);
    
        
        UserModel User = new UserModel
        {
            Id = Guid.NewGuid(),
            Email =  model.Email,
            PasswordHash = Authenticator.HashPassword(null, model.Password),
            IsAdmin = false,
        };

        try
        {
            _db.Add(User);
        }
        catch (DbUpdateException e)
        {
            ModelState.AddModelError(string.Empty, "User with this email already exists");
        }
        
        await _db.SaveChangesAsync();
        return View("Index");
    }

    public IActionResult Login()
    {
        return RedirectToAction("Index", "Auth");
        
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}