using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportTicketSystem.data;
using System;
using System.Threading.Tasks;

namespace SupportTicketSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly TicketAccess _tickets;

    public AdminController(TicketAccess tickets)
    {
        _tickets = tickets;
    }

    // GET /Admin
    public async Task<IActionResult> Index(string sortBy = "severity")
    {
        ViewBag.SortBy = sortBy;
        return View(await _tickets.GetAdminIndex(sortBy));
    }

    // POST /Admin/Assign
    [HttpPost]
    public async Task<IActionResult> Assign(int ticketId, Guid assignedToUserId)
    {
        var result = await _tickets.Assign(ticketId, assignedToUserId);
        if (result == TicketActionResult.NotFound)
            TempData["Error"] = "Ticket not found.";
        else if (result == TicketActionResult.InvalidTransition)
            TempData["Error"] = "Cannot assign a completed ticket.";

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Complete
    [HttpPost]
    public async Task<IActionResult> Complete(int ticketId)
    {
        var result = await _tickets.Complete(ticketId);
        if (result == TicketActionResult.NotFound)
            TempData["Error"] = "Ticket not found.";
        else if (result == TicketActionResult.InvalidTransition)
            TempData["Error"] = "Ticket is already completed.";

        return RedirectToAction(nameof(Index));
    }
}
