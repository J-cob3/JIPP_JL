using Microsoft.EntityFrameworkCore;
using Api.Models;

namespace Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserTask> UserTasks => Set<UserTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder=>
        {
            builder.ToTable("Users");
            builder.HasKey(user => user.Id);
            builder.HasIndex(user => user.Username).IsUnique();
            builder.HasIndex(user => user.Email).IsUnique();
            builder.Property(user => user.Email).IsRequired().HasMaxLength(200);
            builder.Property(user => user.PasswordHash).IsRequired().HasMaxLength(512);
            

            builder.HasMany(user => user.Tasks)
                   .WithOne(task => task.User!)
                   .HasForeignKey(task => task.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<UserTask>(builder =>
    {
        builder.ToTable("Tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.DueDate);
    });
    }
}