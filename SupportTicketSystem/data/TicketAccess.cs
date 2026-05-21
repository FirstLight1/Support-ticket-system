using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.data;

/// <summary>
/// Outcome of a mutating ticket operation. Expected results are returned, not thrown —
/// genuine database faults still bubble up to the global exception handler.
/// </summary>
public enum TicketActionResult
{
    Ok,
    NotFound,
    InvalidTransition
}

/// <summary>
/// The single place ticket queries and status transitions live. Controllers go through
/// this module instead of touching <see cref="AppDbContext"/>, so the Owner rule and the
/// ticket status state machine have one home. See CONTEXT.md for the domain terms.
/// </summary>
public class TicketAccess
{
    private readonly AppDbContext _db;

    public TicketAccess(AppDbContext db)
    {
        _db = db;
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>The Owner's tickets, split into active and completed; active list sorted.</summary>
    public async Task<TicketsIndexModel> GetUserTickets(Guid userId, string sort)
    {
        var active = ApplySort(
            _db.Tickets.Where(t => t.CreatedByUserId == userId &&
                                   t.Status != TicketStatusEnum.Completed),
            sort);

        var completed = _db.Tickets.Where(t => t.CreatedByUserId == userId &&
                                               t.Status == TicketStatusEnum.Completed);

        return new TicketsIndexModel
        {
            ActiveTickets = await active.ToListAsync(),
            CompletedTickets = await completed.ToListAsync()
        };
    }

    /// <summary>
    /// The Admin queue, split into active (Unassigned or Inprogress) and completed tickets —
    /// each sorted — plus the admin list. Creator and assignee are eager-loaded for display.
    /// </summary>
    public async Task<AdminIndexViewModel> GetAdminIndex(string sort)
    {
        var all = _db.Tickets
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo);

        var active = ApplySort(
            all.Where(t => t.Status == TicketStatusEnum.Unassigned ||
                           t.Status == TicketStatusEnum.Inprogress),
            sort);

        var completed = ApplySort(
            all.Where(t => t.Status == TicketStatusEnum.Completed),
            sort);

        return new AdminIndexViewModel
        {
            ActiveTickets = await active.ToListAsync(),
            CompletedTickets = await completed.ToListAsync(),
            Admins = await _db.Users.Where(u => u.IsAdmin).ToListAsync()
        };
    }

    /// <summary>
    /// A ticket the user is allowed to open: their own, or — when <paramref name="isAdmin"/> —
    /// any ticket. Returns <c>null</c> when the ticket is missing OR not permitted; the two
    /// cases are deliberately indistinguishable so callers can't probe ids.
    /// </summary>
    public async Task<Tickets?> GetForUser(int id, Guid userId, bool isAdmin)
    {
        return await _db.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == id &&
                                      (isAdmin || t.CreatedByUserId == userId));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Persists a new ticket as Unassigned, owned by <paramref name="ownerId"/>.</summary>
    public async Task Create(Tickets ticket, Guid ownerId)
    {
        ticket.CreatedByUserId = ownerId;
        ticket.Status = TicketStatusEnum.Unassigned;
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Applies the editable fields of <paramref name="changes"/> to the user's own ticket.
    /// Image path is only overwritten when a new image was uploaded; Status and ownership
    /// are never touched.
    /// </summary>
    public async Task<TicketActionResult> UpdateEditable(int id, Guid userId, bool isAdmin, EditTicketModel changes)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == id &&
                                      (isAdmin || t.CreatedByUserId == userId));
        if (ticket == null)
            return TicketActionResult.NotFound;

        ticket.Predmet = changes.Predmet;
        ticket.TicketText = changes.TicketText;
        ticket.TicketType = changes.TicketType;
        ticket.Severity = changes.Severity;
        if (changes.ImagePath != null)
            ticket.ImagePath = changes.ImagePath;

        await _db.SaveChangesAsync();
        return TicketActionResult.Ok;
    }

    /// <summary>Deletes the user's own ticket (or any ticket when <paramref name="isAdmin"/>). Missing or not permitted → NotFound.</summary>
    public async Task<TicketActionResult> Delete(int id, Guid userId, bool isAdmin)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.TicketId == id &&
                                      (isAdmin || t.CreatedByUserId == userId));
        if (ticket == null)
            return TicketActionResult.NotFound;

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return TicketActionResult.Ok;
    }

    /// <summary>Assigns a ticket to an admin and moves it to Inprogress. Rejected if Completed.</summary>
    public async Task<TicketActionResult> Assign(int id, Guid adminId)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return TicketActionResult.NotFound;
        if (ticket.Status == TicketStatusEnum.Completed)
            return TicketActionResult.InvalidTransition;

        ticket.AssignedToUserId = adminId;
        ticket.Status = TicketStatusEnum.Inprogress;
        await _db.SaveChangesAsync();
        return TicketActionResult.Ok;
    }

    /// <summary>
    /// Closes a ticket, optionally recording a completion note. Allowed from Unassigned or
    /// Inprogress; rejected if already Completed.
    /// </summary>
    public async Task<TicketActionResult> Complete(int id, string? completionNote)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return TicketActionResult.NotFound;
        if (ticket.Status == TicketStatusEnum.Completed)
            return TicketActionResult.InvalidTransition;

        ticket.Status = TicketStatusEnum.Completed;
        ticket.CompletionNote = completionNote;
        await _db.SaveChangesAsync();
        return TicketActionResult.Ok;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static IQueryable<Tickets> ApplySort(IQueryable<Tickets> query, string sort) =>
        sort switch
        {
            "severity" => query.OrderByDescending(t => t.Severity),
            "type" => query.OrderByDescending(t => t.TicketType),
            "date" => query.OrderByDescending(t => t.DatumVytvorenia),
            _ => query.OrderByDescending(t => t.DatumVytvorenia)
        };
}
