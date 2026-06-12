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

    public async Task<Result<AuthResult>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == email, ct);

        if (exists)
        {
            return Result<AuthResult>.Failure(
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

        return Result<AuthResult>.Success(new AuthResult(token, ToProfileDto(user)));
    }

    public async Task<Result<AuthResult>> LoginAsync(
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
        
        return Result<AuthResult>.Success(new AuthResult(token, ToProfileDto(user)));
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return Result<UserProfileDto>.Failure(
                new Error("USER_NOT_FOUND", "User not found,"));
        }

        return Result<UserProfileDto>.Success(ToProfileDto(user));
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return Result<UserProfileDto>.Failure(
                new Error("USER_NOT_FOUND", "User not found,"));
        }
        
        user.UpdateProfile(request.FullName, request.Headline);
        
        await _dbContext.SaveChangesAsync(ct);
        
        return Result<UserProfileDto>.Success(ToProfileDto(user));
    }

    private static Result<AuthResult> InvalidCredentials()
    {
        return Result<AuthResult>.Failure(
            new Error("INVALID_CREDENTIALS", "Invalid email or password."));
    }

    private static UserProfileDto ToProfileDto(User user)
    {
        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Headline,
            user.CreatedAt);
    }
}