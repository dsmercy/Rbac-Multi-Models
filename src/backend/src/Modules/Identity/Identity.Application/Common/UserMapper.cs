using Identity.Application.Common;
using Identity.Domain.Entities;

namespace Identity.Application.Common;

public static class UserMapper
{
    public static UserDto ToDto(User user) => new(
        user.Id,
        user.TenantId,
        user.Email.Value,
        user.DisplayName.Value,
        user.IsActive,
        user.CreatedAt,
        user.LastLoginAt);
}
