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

public sealed record AuthResponse(
    string Token
);
  
    