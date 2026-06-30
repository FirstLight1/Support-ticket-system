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
    public async Task<IActionResult> Create(CreateTicketModel input)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Invalid ticket");
            return View(input);
        }

        var userId = User.GetUserId();
        if (userId == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid user");
            _logger.LogWarning("User Id not attached");
            return View(input);
        }

        var ticket = new Tickets
        {
            Predmet = input.Predmet,
            TicketType = input.TicketType,
            TicketText = input.TicketText,
            Severity = input.Severity,
            RelatedProject = input.RelatedProject
        };

        if (input.Image != null)
        {
            if (!await TryStoreImage(input.Image, path => ticket.ImagePath = path))
                return View(input);
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
            ReturnUrl = LocalReturnUrl(returnUrl)
        };

        ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
        return View(editTicket);
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mutation")]
    public async Task<IActionResult> Edit([Bind("Predmet,TicketType,TicketText,Severity,RelatedProject,Image")] EditTicketModel ticket, int id, string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
            return View(ticket);
        }

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (ticket.Image != null)
        {
            if (!await TryStoreImage(ticket.Image, path => ticket.ImagePath = path))
            {
                ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
                return View(ticket);
            }
        }

        var result = await _tickets.UpdateEditable(id, userId.Value, User.IsInRole("Admin"), ticket);
        if (result == TicketActionResult.NotFound)
        {
            _logger.LogWarning("Edit failed: ticket {TicketId} not found for user {UserId}", id, userId.Value);
            ModelState.AddModelError(string.Empty, "Ticket not found");
            ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
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

        ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
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

    // Sanitizes a returnUrl for rendering in a view's href. A crafted ?returnUrl=https://evil.com
    // would otherwise render as <a href="https://evil.com"> — an open redirect on click.
    private string LocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Tickets")!;

    // Image upload validation/storage is still controller-side (see Candidate 2 in the
    // architecture review). Returns false on any failure (size, format, or IO); the
    // caller is expected to return the view so the user sees the ModelState error.
    private async Task<bool> TryStoreImage(Microsoft.AspNetCore.Http.IFormFile image, Action<string> setPath)
    {
        if (image.Length > 1024 * 1024 * 5)
        {
            _logger.LogWarning("Rejected image upload: {Length} bytes exceeds 5 MB limit", image.Length);
            ModelState.AddModelError(string.Empty, "Image too large (max 5 MB).");
            return false;
        }

        var newFileName = Guid.NewGuid().ToString();
        var imgUploadUtil = new ImageUploadUtil(image, newFileName, _env);
        try
        {
            setPath(await imgUploadUtil.ValidateImage(image));
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to store uploaded image {FileName}", image.FileName);
            ModelState.AddModelError(string.Empty, "Image could not be uploaded. Check the file is a valid JPEG, PNG, BMP, or GIF.");
            return false;
        }
    }
}
