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
        var tickets = _db.Tickets.ToList();
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
    
    
}