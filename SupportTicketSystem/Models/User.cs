using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SupportTicketSystem.Models
{
    /// <summary>
    /// Database entity
    /// </summary>
    public class User
    {
        //Sqlite nema uuid, preto ho vytvorime tu
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required] 
        [EmailAddress] 
        public string Email { get; set; }

        [Required] 
        public string PasswordHash { get; set; }

        public bool IsAdmin { get; set; }
        
        public ICollection<Ticket> CreatedTickets { get; set; }
        public ICollection<Ticket> AssignedTickets { get; set; }
    }

    /// <summary>
    /// Model used registration
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>
    /// Model for logging in
    /// </summary>
    public class LoginViewModel
    {
        [Required] [EmailAddress] public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]

    public string Password { get; set; }
    }
}