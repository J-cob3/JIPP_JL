using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Endpoints;

public static class TasksEndpoints
{
    public static void MapTasks(this WebApplication app)
    {
        app.MapPost("/tasks", async (AppDbContext db, CreateTaskDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return Results.BadRequest("Title is required.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId);
            if (user is null) return Results.NotFound($"User {dto.UserId} not found.");

            var task = new UserTask
            {
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim(),
                DueDate = dto.DueDate,
                UserId = dto.UserId
            };

            db.UserTasks.Add(task);
            await db.SaveChangesAsync();
            return Results.Created($"/tasks/{task.Id}", task);
        })
        .RequireAuthorization();
    }
}