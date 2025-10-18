using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

// Podłączenie bazy danych
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=JippJl;Trusted_Connection=True;TrustServerCertificate=True"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Health
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/hello/{name}", (string name) =>
{
    return Results.Ok($"Hello, {name}!");
});

app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users.AsNoTracking().ToListAsync();
    return Results.Ok(users);
});

// Users: dodaj
app.MapPost("/users", async (AppDbContext db, UserDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email))
        return Results.BadRequest("Username and Email are required.");

    var user = new User { Username = dto.Username.Trim(), Email = dto.Email.Trim() };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

app.Run();
 class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Konfiguracja EF Core
class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
        });
    }
}

record UserDto(string Username, string Email);

public partial class Program { } 