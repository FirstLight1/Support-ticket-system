# Support ticket system

A support desk where users raise tickets and admins triage them. This file fixes the
domain vocabulary so code, views, and future refactors stay consistent. Models
deliberately use Czech names (see CLAUDE.md); the terms below name the English concepts
those map to.

## Language

**Ticket**:
A support request raised by a user. Carries a subject (`Predmet`), body (`TicketText`),
type, severity, status, and an optional image.
_Avoid_: issue, case, request

**Owner**:
The user who created a ticket (`CreatedByUserId`). A ticket has exactly one Owner. A user
may only read or change their own tickets; admins act on any ticket through the Admin queue.
_Avoid_: author, reporter, creator (use Owner when the access rule is the point)

**Assignee**:
The admin a ticket is assigned to (`AssignedToUserId`). Absent until the ticket is assigned.

**Ticket status**:
The lifecycle state of a ticket. Three states, in order: **Unassigned** → **Inprogress** →
**Completed**. Spelled `Inprogress` (one word) in code. Completed is terminal — there is no reopen.
_Avoid_: open/closed, new/done

**Admin queue**:
The tickets needing admin attention — those that are Unassigned or Inprogress. Completed
tickets drop out. This is what `AdminController.Index` shows.
_Avoid_: backlog, inbox

**Ticket access**:
The single module that owns every ticket query and status transition, so no controller
touches the database directly for ticket work. The Owner rule and the Ticket status
transitions live here, in one place.
_Avoid_: repository, service (it is deeper than a repository — it owns transitions and the
Owner rule, not just storage)

### Status transitions

- **Assign** (admin picks an Assignee): Unassigned or Inprogress → Inprogress. Rejected on a Completed ticket.
- **Complete** (admin closes a ticket): Unassigned or Inprogress → Completed. Rejected only if already Completed (lets admins close spam/duplicates without assigning).

## Example dialogue

**Dev:** When an admin opens the queue, do they see completed tickets?
**Expert:** No — the Admin queue is only Unassigned and Inprogress. Once a ticket is
Completed it leaves the queue and shows up in the Owner's Completed list.

**Dev:** Can a user edit someone else's ticket if they know the id?
**Expert:** Never. Only the Owner reads or changes a ticket. That rule lives in Ticket
access, so every read and edit goes through it.

**Dev:** Can an admin complete a ticket that was never assigned?
**Expert:** Yes — completing is allowed straight from Unassigned, so admins can close
spam or duplicates without assigning. The only thing Complete rejects is an
already-Completed ticket.
