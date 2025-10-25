using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsers(this WebApplication app)
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
    }
}