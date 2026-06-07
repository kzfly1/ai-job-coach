using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Auth.Domain;
using AIJobCoach.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Modules.Auth.Application;

public sealed class AuthService
{
    private const int BcryptWorkFactor = 12;

    private readonly AppDbContext _dbContext;
    private readonly JwtService _jwtService;

    public AuthService(AppDbContext dbContext, JwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == email, ct);

        if (exists)
        {
            return Result<AuthResponse>.Failure(
                new Error("EMAIL_ALREADY_EXISTS", "An account with this email already exists."));
        }
        
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            request.Password,
            workFactor: BcryptWorkFactor);

        var user = new User(
            email,
            passwordHash,
            request.FullName.Trim(),
            request.Headline);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        var token = _jwtService.GenerateToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(token));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var isValidPassword = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!isValidPassword)
        {
            return InvalidCredentials();
        }

        var token = _jwtService.GenerateToken(user);
        
        return Result<AuthResponse>.Success(new AuthResponse(token));
    }

    private static Result<AuthResponse> InvalidCredentials()
    {
        return Result<AuthResponse>.Failure(
            new Error("INVALID_CREDENTIALS", "Invalid email or password."));
    }
}