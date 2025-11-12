namespace Api.Models;

public record AuthResponseDto(string Token, DateTime ExpiresAt);