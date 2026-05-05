using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

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
    public IActionResult Index()
    {
        var tickets = _db.Tickets
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Where(t => t.Status == TicketStatusEnum.Unassigned ||
                        t.Status == TicketStatusEnum.Inprogress)
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.Severity)
            .ToList();

        var admins = _db.Users
            .Where(u => u.IsAdmin)
            .ToList();

        return View(new AdminIndexViewModel
        {
            Tickets = tickets,
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
}