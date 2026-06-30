using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SupportTicketSystem.data;
using SupportTicketSystem.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using SupportTicketSystem.Utils;

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
    private readonly TicketAccess _tickets;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(TicketAccess tickets, IWebHostEnvironment env,  ILogger<TicketsController> logger)
    {
        _tickets = tickets;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index(string sortBy = "date")
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            _logger.LogWarning("Unauthorized logging in");
            return Unauthorized();
        }

        ViewBag.SortBy = sortBy;
        return View(await _tickets.GetUserTickets(userId.Value, sortBy));
    }

    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Create(Tickets ticket)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Invalid ticket");
            return View(ticket);
        }

        var userId = User.GetUserId();
        if (userId == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid user");
            _logger.LogWarning("User Id not attached");
            return View(ticket);
        }

        if (ticket.Image != null)
        {
            await TryStoreImage(ticket.Image, path => ticket.ImagePath = path);
            _logger.LogInformation("Image stored at {ImagePath} for new ticket", ticket.ImagePath);
        }

        await _tickets.Create(ticket, userId.Value);
        _logger.LogInformation("Ticket {TicketId} created by user {UserId}", ticket.TicketId, userId.Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(int? id, string? returnUrl)
    {
        if (id == null) return NotFound();

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var ticket = await _tickets.GetForUser(id.Value, userId.Value, User.IsInRole("Admin"));
        if (ticket == null) return NotFound();

        if (ticket.Status == TicketStatusEnum.Completed)
            return RedirectToAction(nameof(Details), new { id = ticket.TicketId, returnUrl });

        var editTicket = new EditTicketModel
        {
            TicketId = ticket.TicketId,
            Predmet = ticket.Predmet,
            Severity = ticket.Severity,
            TicketText = ticket.TicketText,
            TicketType = ticket.TicketType,
            ImagePath = ticket.ImagePath,
            RelatedProject = ticket.RelatedProject,
            ReturnUrl = returnUrl
        };

        ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Tickets");
        return View(editTicket);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Edit(EditTicketModel ticket, int id, string? returnUrl)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (ticket.Image != null)
        {
            await TryStoreImage(ticket.Image, path => ticket.ImagePath = path);
        }

        var result = await _tickets.UpdateEditable(id, userId.Value, User.IsInRole("Admin"), ticket);
        if (result == TicketActionResult.NotFound)
        {
            _logger.LogWarning("Edit failed: ticket {TicketId} not found for user {UserId}", id, userId.Value);
            ModelState.AddModelError(string.Empty, "Ticket not found");
            return View(ticket);
        }

        _logger.LogInformation("Ticket {TicketId} edited by user {UserId}", id, userId.Value);
        return RedirectBack(returnUrl ?? ticket.ReturnUrl);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Details(int id, string? returnUrl)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var ticket = await _tickets.GetForUser(id, userId.Value, User.IsInRole("Admin"));
        if (ticket == null) return NotFound();

        ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Tickets");
        return View(ticket);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Delete(int ticketId, string? returnUrl)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _tickets.Delete(ticketId, userId.Value, User.IsInRole("Admin"));
        if (result == TicketActionResult.NotFound)
        {
            _logger.LogWarning("Delete failed: ticket {TicketId} not found for user {UserId}", ticketId, userId.Value);
            return NotFound();
        }

        _logger.LogInformation("Ticket {TicketId} deleted by user {UserId}", ticketId, userId.Value);
        return RedirectBack(returnUrl);
    }

    // Returns to the supplied URL after a mutation, falling back to the ticket list. Only
    // local URLs are honored so a crafted returnUrl can't turn this into an open redirect.
    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));

    // Image upload validation/storage is still controller-side (see Candidate 2 in the
    // architecture review). On failure it logs and leaves the path unchanged.
    private async Task TryStoreImage(Microsoft.AspNetCore.Http.IFormFile image, Action<string> setPath)
    {
        if (image.Length > 1024 * 1024 * 5)
        {
            _logger.LogWarning("Rejected image upload: {Length} bytes exceeds 5 MB limit", image.Length);
            ModelState.AddModelError(string.Empty, "Image too large");
        }

        var newFileName = Guid.NewGuid().ToString();
        var imgUploadUtil = new ImageUploadUtil(image, newFileName, _env);
        try
        {
            setPath(await imgUploadUtil.ValidateImage(image));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to store uploaded image {FileName}", image.FileName);
        }
    }
}
