using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SupportTicketSystem.Utils;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
    private readonly IWebHostEnvironment _env;

    public TicketsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }
    // GET
    [HttpGet]
    [Authorize]
    public IActionResult Index(string sortBy = "date")
    {
        var userId = User.GetUserId();
        var query = _db.Tickets.Where(t => t.CreatedByUserId == userId).Where(t => t.Status != TicketStatusEnum.Completed);
        var completedTickets = _db.Tickets.Where(t => t.CreatedByUserId == userId).Where(t => t.Status == TicketStatusEnum.Completed).ToList();
        query = sortBy switch
        {
            "severity" => query.OrderByDescending(t => t.Severity),
            "type"     => query.OrderByDescending(t => t.TicketType),
            "date"     => query.OrderByDescending(t => t.DatumVytvorenia),
            _          => query.OrderByDescending(t => t.DatumVytvorenia)
        };
        ViewBag.SortBy = sortBy;
        
        return View(new TicketsIndexModel(){ActiveTickets = query.ToList(), CompletedTickets = completedTickets});
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

        if (ticket.Image != null)
        {
            long size = ticket.Image.Length;

            if (size > 1024 * 1024 * 5)
            {
                ModelState.AddModelError(string.Empty, "Image too large");
            }

            string newFileName = Guid.NewGuid().ToString();
            var imgUploadUtil = new ImageUploadUtil(ticket.Image, newFileName, _env);
            
            try
            {
                string imgFilePath = await imgUploadUtil.ValidateImage(ticket.Image);
                ticket.ImagePath = imgFilePath;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
            ImagePath =  ticket.ImagePath,
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
            if (ticket.Image != null)
            {
                long size = ticket.Image.Length;

                if (size > 1024 * 1024 * 5)
                {
                    ModelState.AddModelError(string.Empty, "Image too large");
                }

                string newFileName = Guid.NewGuid().ToString();
                var imgUploadUtil = new ImageUploadUtil(ticket.Image, newFileName, _env);
            
                try
                {
                    string imgFilePath = await imgUploadUtil.ValidateImage(ticket.Image);
                    ticket.ImagePath = imgFilePath;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            
            
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