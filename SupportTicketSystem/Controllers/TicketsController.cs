using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;

namespace SupportTicketSystem.Controllers;

public class TicketsController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}