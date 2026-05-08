using System.Collections.Generic;

namespace SupportTicketSystem.Models;

public class AdminIndexViewModel
{
    public List<Tickets> ActiveTickets { get; set; }
    public List<Tickets> CompletedTickets { get; set; }
    public List<UserModel> Admins { get; set; }
}
