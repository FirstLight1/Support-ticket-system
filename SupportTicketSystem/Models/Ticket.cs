using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace SupportTicketSystem.Models;
public enum TicketType
{
    BugReport,
    Feature,
    Question
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}
    
public class Ticket
{
    public int TicketId { get; set; }
        
    [Required]
    [MaxLength(100)]
    public string Predmet {get; set;}
        
    public string DatumVytvorenia {get; set;} =  DateTime.UtcNow.ToString("o");
        
    [Required]
    public TicketType TicketType { get; set; }
        
    [Required]
    public string TicketText { get; set; }
        
    [Required]
    public Severity Severity { get; set; }

    public Guid CreatedByUserId { get; set; }
    public UserModel CreatedBy { get; set; }

    public Guid? AssignedToUserId {get; set;}
    public UserModel? AssignedTo {get; set;}
} 
