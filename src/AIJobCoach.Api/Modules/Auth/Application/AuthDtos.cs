namespace AIJobCoach.Api.Modules.Auth.Application;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? Headline
);
    
public sealed record LoginRequest(
    string Email,
    string Password
);

public sealed record AuthResult(
    string Token,
    UserProfileDto User);

public sealed record AuthResponse(
    UserProfileDto User
);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string? Headline,
    DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(
    string FullName,
    string? Headline);
  
    