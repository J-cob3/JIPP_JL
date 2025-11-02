namespace Api.Models;

public record CreateTaskDto(int UserId, string Title, string? Description, DateTime? DueDate);