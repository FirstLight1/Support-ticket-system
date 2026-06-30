using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SupportTicketSystem.Models
{
    /// <summary>
    /// Database entity
    /// </summary>
    [Index(nameof(Email), IsUnique = true)]
    public class UserModel
    {
        //Sqlite nema uuid, preto ho vytvorime tu
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required] 
        [EmailAddress]
        public required string Email { get; set; }

        [Required] 
        public required string PasswordHash { get; set; }

        public bool IsAdmin { get; set; }

        // Account lockout: after LockoutThreshold failed logins (see appsettings Security:Lockout),
        // LockoutEnd blocks further attempts until it elapses. Reset on successful login.
        public int FailedLoginAttempts { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public ICollection<Tickets> CreatedTickets { get; set; }
        public ICollection<Tickets> AssignedTickets { get; set; }
    }

    /// <summary>
    /// Model used registration
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public required string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
        public required string ConfirmPassword { get; set; }
    }

    /// <summary>
    /// Model for logging in
    /// </summary>
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
         public required string  Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public required string Password { get; set; }
    }
}