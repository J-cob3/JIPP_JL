namespace Api.Models;

public class UserTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
}