using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = string.Empty;

    public List<UserTask> Tasks { get; set; } = [];
}