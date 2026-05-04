using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.Controllers;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

public class TicketsController : Controller
{
    private readonly AppDbContext _db;
    
    public TicketsController(AppDbContext db)
    {
        _db = db;
    }
    // GET
    [HttpGet]
    [Authorize]
    public IActionResult Index()
    {
        var userId = User.GetUserId();
        var tickets = _db.Tickets.Where(t => t.CreatedByUserId == userId).ToList();
        return View(tickets);
    }

    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(Tickets ticket)
    {
        var userId = User.GetUserId();

        if (userId != null)
        {
            ticket.CreatedByUserId = (Guid)userId;
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid user");
        }
        
        try
        {
            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize]
    public IActionResult Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }
        
        var ticket = _db.Tickets.Find(id);

        if (ticket == null)
        { 
            return NotFound();
        }
        EditTicketModel editTicket = new EditTicketModel
        {
            TicketId =  ticket.TicketId,
            Predmet =  ticket.Predmet,
            Severity = ticket.Severity,
            TicketText =  ticket.TicketText,
            TicketType =  ticket.TicketType,
        };
        
        return View(editTicket);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Edit(EditTicketModel ticket, int id)
    {
        var ticketToEdit = await _db.Tickets.FindAsync(id);
        if (ticketToEdit != null)
        {
            ticket.TicketId = ticketToEdit.TicketId;
            _db.Entry(ticketToEdit).CurrentValues.SetValues(ticket);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        
        ModelState.AddModelError(string.Empty, "Ticket not found");
        return View(ticket);

    }

    public IActionResult Details(int id)
    {
        var ticket = _db.Tickets.Find(id);
        if (ticket == null)
        {
            return NotFound();
        }
        return View(ticket);
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Delete(int ticketId)
    {
        Tickets? ticket = await _db.Tickets.FindAsync(ticketId);
        
        if (ticket != null)
        {
            try
            {
                _db.Tickets.Remove(ticket);
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Ticket not found");
        }
        
        return RedirectToAction(nameof(Index));
    }
    
    
}