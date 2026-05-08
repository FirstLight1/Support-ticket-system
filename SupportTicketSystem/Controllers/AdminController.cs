using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SupportTicketSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // GET /Admin
    public IActionResult Index(string sortBy = "severity")
    {
        var query = _db.Tickets
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo);

        var sorted = sortBy switch
        {
            "severity" => query.OrderByDescending(t => t.Severity),
            "type" => query.OrderByDescending(t => t.TicketType),
            "date" => query.OrderByDescending(t => t.DatumVytvorenia),
            _ => query.OrderByDescending(t => t.Severity)
        };

        var allTickets = sorted.ToList();
        var admins = _db.Users.Where(u => u.IsAdmin).ToList();

        ViewBag.SortBy = sortBy;
        return View(new AdminIndexViewModel
        {
            ActiveTickets = allTickets
                .Where(t => t.Status == TicketStatusEnum.Unassigned ||
                            t.Status == TicketStatusEnum.Inprogress)
                .ToList(),
            CompletedTickets = allTickets
                .Where(t => t.Status == TicketStatusEnum.Completed)
                .ToList(),
            Admins = admins
        });
    }

    // POST /Admin/Assign
    [HttpPost]
    public async Task<IActionResult> Assign(int ticketId, Guid assignedToUserId)
    {
        var ticket = await _db.Tickets.FindAsync(ticketId);

        if (ticket == null)
            return NotFound();

        ticket.AssignedToUserId = assignedToUserId;
        ticket.Status = TicketStatusEnum.Inprogress;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Admin/Complete
    public async Task<IActionResult> Complete(int ticketId, string? completionNote)
    {
        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound();

        ticket.Status = TicketStatusEnum.Completed;
        ticket.CompletionNote = completionNote;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
        return RedirectToAction(nameof(Index));
    }
}