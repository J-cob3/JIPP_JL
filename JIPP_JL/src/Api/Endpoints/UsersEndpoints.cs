using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations; // Potrzebne do walidacji
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;
using Api.Mappers;
using AutoMapper; // Dodaj namespace

namespace Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsers(this WebApplication app, byte[] jwtKey, string? jwtIssuer, string? jwtAudience)
    {
        // Zmiana: wstrzykujemy IMapper
        app.MapGet("/users", async (AppDbContext db, IMapper mapper) =>
        {
            var users = await db.Users.AsNoTracking().ToListAsync();
            return Results.Ok(mapper.Map<List<UserDto>>(users));
        });

        // NOWY ENDPOINT: Raport
        app.MapGet("/reports/new-users", async (DateTime? from, DateTime? to, AppDbContext db, IMapper mapper) =>
        {
            var query = db.Users.AsNoTracking().AsQueryable();

            if (from.HasValue)
                query = query.Where(u => u.CreatedAt >= from.Value);
            
            if (to.HasValue)
                query = query.Where(u => u.CreatedAt <= to.Value);

            var users = await query.ToListAsync();
            return Results.Ok(mapper.Map<List<UserDto>>(users));
        });

        app.MapPost("/users", async (AppDbContext db, UserDto dto, ILogger<Program> logger, IMapper mapper) =>
        {
            // Ręczna walidacja DataAnnotations
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(dto);
            if (!Validator.TryValidateObject(dto, validationContext, validationResults, true))
            {
                return Results.BadRequest(validationResults.Select(x => x.ErrorMessage));
            }

            var user = new User 
            { 
                Username = dto.Username.Trim(), 
                Email = dto.Email.Trim(),
                CreatedAt = DateTime.UtcNow // Ustawiamy datę
            };
            
            db.Users.Add(user);
            await db.SaveChangesAsync();
            
            logger.LogInformation("Utworzono nowego użytkownika: {Username} ({Email})", user.Username, user.Email);

            // Użycie mappera w odpowiedzi
            return Results.Created($"/users/{user.Id}", mapper.Map<UserDto>(user));
        });

        app.MapGet("/users/{id}", async (int id, AppDbContext db, IMapper mapper) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return user is null ? Results.NotFound() : Results.Ok(mapper.Map<UserDto>(user));
        });

        app.MapPut("/users/{id}", async (int id, AppDbContext db, UserDto dto, ILogger<Program> logger) =>
        {
            // Walidacja
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true))
            {
                return Results.BadRequest(validationResults.Select(x => x.ErrorMessage));
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();

            user.Username = dto.Username.Trim();
            user.Email = dto.Email.Trim();

            try
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Zaktualizowano użytkownika ID: {Id}", id);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Błąd podczas aktualizacji użytkownika ID: {Id}", id);
                return Results.StatusCode(500);
            }

            return Results.NoContent();
        });

        app.MapDelete("/users/{id}", async (int id, AppDbContext db, ILogger<Program> logger) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            
            logger.LogInformation("Usunięto użytkownika ID: {Id}", id);
            return Results.NoContent();
        });

        app.MapGet("/users/{id}/tasks", async (int id, AppDbContext db) =>
        {
            var tasks = await db.UserTasks.AsNoTracking()
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

        app.MapPost("/auth/login", async (AppDbContext db, LoginDto dto, IPasswordHasher<User> hasher, ILogger<Program> logger) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return Results.BadRequest("Username and Password are required.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user is null)
            {
                logger.LogWarning("Nieudana próba logowania - nieznany użytkownik: {Username}", dto.Username);
                return Results.Unauthorized();
            }

            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                logger.LogWarning("Nieudana próba logowania - błędne hasło: {Username}", dto.Username);
                return Results.Unauthorized();
            }

            var expires = DateTime.UtcNow.AddHours(1);
            var token = CreateToken(user, jwtKey, jwtIssuer, jwtAudience, expires);
            
            logger.LogInformation("Użytkownik zalogowany: {Username}", dto.Username);

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