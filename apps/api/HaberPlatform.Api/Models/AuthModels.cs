namespace HaberPlatform.Api.Models;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string[] Roles
);

public record CreateUserRequest(
    string Email,
    string DisplayName,
    string TempPassword,
    string[] Roles
);

public record UserListDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    string[] Roles
);

