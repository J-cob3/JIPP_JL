using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public class UserDto
{
    public int Id { get; set; }

    [Required]
    public string Username { get; set; } = default!;

    [Required]
    [EmailAddress(ErrorMessage = "Niepoprawny format adresu e-mail.")]
    public string Email { get; set; } = default!;
}