using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

    public IActionResult Terms()
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

    // GET /Home/Profile
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        var ticketCount = await _db.Tickets.CountAsync(t => t.CreatedByUserId == userId.Value);
        ViewBag.TicketCount = ticketCount;
        return View(user);
    }

    // GET /Home/ExportMyData
    [HttpGet]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> ExportMyData()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        var tickets = await _db.Tickets
            .Where(t => t.CreatedByUserId == userId.Value)
            .Select(t => new
            {
                t.TicketId,
                t.Predmet,
                t.TicketText,
                t.TicketType,
                t.Severity,
                t.Status,
                t.DatumVytvorenia,
                t.RelatedProject,
                t.CompletionNote
            })
            .ToListAsync();

        var export = new
        {
            User = new { user.Email, user.IsAdmin, user.IsActive },
            Tickets = tickets,
            ExportedAt = DateTimeOffset.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        _logger.LogInformation("User {UserId} exported their data", userId);
        return File(bytes, "application/json", $"my-data-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json");
    }

    // POST /Home/DeleteAccount
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        // Unassign tickets assigned to this user (they belong to other users' accounts).
        var assigned = await _db.Tickets.Where(t => t.AssignedToUserId == userId.Value).ToListAsync();
        foreach (var t in assigned)
            t.AssignedToUserId = null;

        // Delete tickets created by this user (their own data).
        var created = await _db.Tickets.Where(t => t.CreatedByUserId == userId.Value).ToListAsync();
        _db.Tickets.RemoveRange(created);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} deleted their account and {TicketCount} tickets", userId, created.Count);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
