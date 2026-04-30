using Microsoft.EntityFrameworkCore;
using SupportTicketSystem.Models;

namespace SupportTicketSystem.data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserModel> Users { get; set; }
    public DbSet<Tickets> Tickets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Priradi PK z Users ako FK do Tickets
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Tickets>()
            .HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTickets)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Tickets>()
            .HasOne(t => t.AssignedTo)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tickets>()
            .HasIndex(t => t.CreatedByUserId);
        
        modelBuilder.Entity<Tickets>()
            .HasIndex(t => t.AssignedToUserId);
    }
    
}