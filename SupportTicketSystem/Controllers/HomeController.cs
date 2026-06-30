using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;
using SupportTicketSystem.Utils;

namespace SupportTicketSystem.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
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
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {

        if (!ModelState.IsValid) return View("Register", model);

        if (Authenticator.FindUser(_db, model.Email) != null)
        {
            _logger.LogWarning("Registration rejected: email {EmailHash} already exists", PiiHash.Email(model.Email));
            ModelState.AddModelError(string.Empty, "User with this email already exists");
            return View("Register", model);
        }

        UserModel User = new UserModel
        {
            Id = Guid.NewGuid(),
            Email =  model.Email,
            PasswordHash = string.Empty,
            IsAdmin = false,
        };
        User.PasswordHash = Authenticator.HashPassword(User, model.Password);

        try
        {
            _db.Add(User);
            await _db.SaveChangesAsync();
            await Authenticator.SignIn(HttpContext, User);
            _logger.LogInformation("New user registered: {UserId}", User.Id);
            return RedirectToAction("Index", "Home");
        }
        catch (DbUpdateException e)
        {
            _logger.LogError(e, "Failed to register user {EmailHash}", PiiHash.Email(model.Email));
            ModelState.AddModelError(string.Empty, "Registration failed, please try again");
            return View("Register", model);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
