using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace SupportTicketSystem.Models;
public enum TicketTypeEnum
{
    BugReport,
    Feature,
    Question
}

public enum SeverityEnum
{
    Low,
    Medium,
    High,
    Critical
}

public enum TicketStatusEnum
{
    Unassigned,
    Inprogress,
    Completed
}
    
public class Tickets
{
    [Key]
    public int TicketId { get; set; }
        
    [Required]
    [MaxLength(100)]
    public string Predmet {get; set;}
        
    public string DatumVytvorenia {get; set;} =  DateTime.UtcNow.ToString("o");
        
    [Required]
    public TicketTypeEnum TicketType { get; set; }
        
    [Required]
    public string TicketText { get; set; }
        
    [Required]
    public SeverityEnum Severity { get; set; }

    public TicketStatusEnum Status { get; set; }

    public string? ImagePath { get; set; } 
    
    [NotMapped]
    public IFormFile? Image { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    public UserModel? CreatedBy { get; set; }

    public Guid? AssignedToUserId {get; set;}
    public UserModel? AssignedTo {get; set;}
    public string? CompletionNote { get; set; }

    // Name of the related GitHub project (see Utils/GitHubProjects). Optional — not every
    // ticket maps to one project. The repo name is stored; the URL is resolved at display time.
    public string? RelatedProject { get; set; }
}

public class EditTicketModel
{
    public int TicketId { get; set; }
    public string Predmet { get; set; }
    public TicketTypeEnum TicketType { get; set; }
    public string TicketText { get; set; }
    
    public string? ImagePath { get; set; }
    public IFormFile? Image { get; set; }
    public SeverityEnum Severity { get; set; }
    public string? RelatedProject { get; set; }
    public string? ReturnUrl { get; set; }
}

public class TicketsIndexModel
{
    public List<Tickets> ActiveTickets { get; set; }
    public List<Tickets> CompletedTickets { get; set; }
}
