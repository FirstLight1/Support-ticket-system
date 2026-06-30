using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;
using SupportTicketSystem.Utils;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SupportTicketSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly TicketAccess _tickets;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminController> _logger;

    public AdminController(TicketAccess tickets, AppDbContext db, ILogger<AdminController> logger)
    {
        _tickets = tickets;
        _db = db;
        _logger = logger;
    }

    // GET /Admin
    public async Task<IActionResult> Index(string sortBy = "severity")
    {
        ViewBag.SortBy = sortBy;
        return View(await _tickets.GetAdminIndex(sortBy));
    }

    // POST /Admin/Assign
    [HttpPost]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Assign(int ticketId, Guid assignedToUserId)
    {
        var result = await _tickets.Assign(ticketId, assignedToUserId);
        if (result == TicketActionResult.NotFound)
        {
            _logger.LogWarning("Assign failed: ticket {TicketId} not found", ticketId);
            TempData["Error"] = "Ticket not found.";
        }
        else if (result == TicketActionResult.InvalidTransition)
        {
            _logger.LogWarning("Assign rejected: ticket {TicketId} is already completed", ticketId);
            TempData["Error"] = "Cannot assign a completed ticket.";
        }
        else
        {
            _logger.LogInformation("Ticket {TicketId} assigned to admin {AdminId}", ticketId, assignedToUserId);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Complete
    [HttpPost]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Complete(int ticketId, string? completionNote)
    {
        var result = await _tickets.Complete(ticketId, completionNote);
        if (result == TicketActionResult.NotFound)
        {
            _logger.LogWarning("Complete failed: ticket {TicketId} not found", ticketId);
            TempData["Error"] = "Ticket not found.";
        }
        else if (result == TicketActionResult.InvalidTransition)
        {
            _logger.LogWarning("Complete rejected: ticket {TicketId} is already completed", ticketId);
            TempData["Error"] = "Ticket is already completed.";
        }
        else
        {
            _logger.LogInformation("Ticket {TicketId} completed", ticketId);
        }

        return RedirectToAction(nameof(Index));
    }

    // GET /Admin/Users
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .OrderByDescending(u => u.IsAdmin)
            .ThenBy(u => u.Email)
            .ToListAsync();
        return View(users);
    }

    // POST /Admin/ToggleActive
    [HttpPost]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> ToggleActive(Guid userId)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId.ToString() == currentUserId)
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin {AdminId} set user {UserId} IsActive={IsActive}", currentUserId, userId, user.IsActive);
        return RedirectToAction(nameof(Users));
    }

    // POST /Admin/ToggleAdmin
    [HttpPost]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> ToggleAdmin(Guid userId)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId.ToString() == currentUserId)
        {
            TempData["Error"] = "You cannot change your own admin role.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        user.IsAdmin = !user.IsAdmin;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin {AdminId} set user {UserId} IsAdmin={IsAdmin}", currentUserId, userId, user.IsAdmin);
        return RedirectToAction(nameof(Users));
    }
}
