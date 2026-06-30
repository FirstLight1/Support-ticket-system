using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SupportTicketSystem.data;
using System;
using System.Threading.Tasks;

namespace SupportTicketSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly TicketAccess _tickets;
    private readonly ILogger<AdminController> _logger;

    public AdminController(TicketAccess tickets, ILogger<AdminController> logger)
    {
        _tickets = tickets;
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
}
