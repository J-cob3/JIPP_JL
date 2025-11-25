using Api.Models;

namespace Api.Mappers;

internal static class UserMappingExtensions
{
    public static UserDto ToDto(this User user) =>
        new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email
        };
}