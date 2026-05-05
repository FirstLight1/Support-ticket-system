using System.Collections.Generic;

namespace SupportTicketSystem.Models;

public class AdminIndexViewModel
{
    public List<Tickets> Tickets { get; set; }
    public List<UserModel> Admins { get; set; }
}
