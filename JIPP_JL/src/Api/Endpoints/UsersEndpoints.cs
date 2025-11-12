using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsers(this WebApplication app, byte[] jwtKey, string? jwtIssuer, string? jwtAudience)
    {
        app.MapGet("/users", async (AppDbContext db) =>
        {
            var users = await db.Users.AsNoTracking().ToListAsync();
            return Results.Ok(users);
        });

        app.MapPost("/users", async (AppDbContext db, UserDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email))
                return Results.BadRequest("Username and Email are required.");

            var user = new User { Username = dto.Username.Trim(), Email = dto.Email.Trim() };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/users/{user.Id}", user);
        });

        app.MapGet("/users/{id}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        app.MapPut("/users/{id}", async (int id, AppDbContext db, UserDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email))
                return Results.BadRequest("Username and Email are required.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();

            user.Username = dto.Username.Trim();
            user.Email = dto.Email.Trim();

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.StatusCode(500);
            }

            return Results.NoContent();
        });

        app.MapDelete("/users/{id}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/users/{id}/tasks", async (int id, AppDbContext db) =>
        {
            var tasks = await db.Tasks.AsNoTracking()
                .Where(t => t.UserId == id)
                .ToListAsync();
            if (!tasks.Any())
            {
                var exists = await db.Users.AnyAsync(u => u.Id == id);
                return exists ? Results.Ok(tasks) : Results.NotFound();
            }
            return Results.Ok(tasks);
        });

        app.MapPost("/auth/register", async (AppDbContext db, RegisterUserDto dto, IPasswordHasher<User> hasher) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return Results.BadRequest("Username, Email and Password are required.");

            if (await db.Users.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email))
                return Results.Conflict("Username or Email already exists.");

            var user = new User
            {
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim()
            };
            user.PasswordHash = hasher.HashPassword(user, dto.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new { user.Id, user.Username, user.Email });
        });

        app.MapPost("/auth/login", async (AppDbContext db, LoginDto dto, IPasswordHasher<User> hasher) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return Results.BadRequest("Username and Password are required.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user is null)
                return Results.Unauthorized();

            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var expires = DateTime.UtcNow.AddHours(1);
            var token = CreateToken(user, jwtKey, jwtIssuer, jwtAudience, expires);

            return Results.Ok(new AuthResponseDto(token, expires));
        });
    }
    private static string CreateToken(User user, byte[] jwtKey, string? issuer, string? audience, DateTime expires)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(jwtKey),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}