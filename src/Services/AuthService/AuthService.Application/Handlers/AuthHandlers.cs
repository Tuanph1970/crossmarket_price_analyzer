using AuthService.Application.DTOs;
using AuthService.Application.Persistence;
using AuthService.Domain.Entities;
using CrossMarket.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Application.Commands;

// ── Register ───────────────────────────────────────────────────────────────────

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher _passwordHasher;

    public RegisterCommandHandler(AuthDbContext db, JwtTokenGenerator tokenGenerator, PasswordHasher passwordHasher)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == cmd.Email.ToLowerInvariant(), ct))
            throw new InvalidOperationException($"A user with email '{cmd.Email}' already exists.");

        var user = User.Create(cmd.Email, _passwordHasher.HashPassword(cmd.Password), cmd.FullName);

        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);

        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var refreshToken = JwtTokenGenerator.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(30);
        user.SetRefreshToken(refreshToken, refreshExpiry);

        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, new[] { "User" });

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(60),
            User: new UserDto(user.Id, user.Email, user.FullName, user.IsEmailConfirmed)
        );
    }
}

// ── Login ─────────────────────────────────────────────────────────────────────

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher _passwordHasher;

    public LoginCommandHandler(AuthDbContext db, JwtTokenGenerator tokenGenerator, PasswordHasher passwordHasher)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == cmd.Email.ToLowerInvariant(), ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is suspended.");

        if (!_passwordHasher.VerifyPassword(cmd.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var refreshToken = JwtTokenGenerator.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(30);
        user.SetRefreshToken(refreshToken, refreshExpiry);
        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, new[] { "User" });

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(60),
            User: new UserDto(user.Id, user.Email, user.FullName, user.IsEmailConfirmed)
        );
    }
}

// ── Refresh Token ─────────────────────────────────────────────────────────────

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher _passwordHasher;

    public RefreshTokenCommandHandler(AuthDbContext db, JwtTokenGenerator tokenGenerator, PasswordHasher passwordHasher)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.RefreshToken == cmd.RefreshToken &&
                u.RefreshTokenExpiresAt > DateTime.UtcNow, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is suspended.");

        // Rotate refresh token
        var newRefresh = JwtTokenGenerator.GenerateRefreshToken();
        var newExpiry = DateTime.UtcNow.AddDays(30);
        user.SetRefreshToken(newRefresh, newExpiry);
        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, new[] { "User" });

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: newRefresh,
            ExpiresAt: DateTime.UtcNow.AddMinutes(60),
            User: new UserDto(user.Id, user.Email, user.FullName, user.IsEmailConfirmed)
        );
    }
}