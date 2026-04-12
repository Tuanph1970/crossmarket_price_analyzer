namespace AuthService.Tests.Application;

using AuthService.Application.Commands;
using AuthService.Application.DTOs;
using AuthService.Application.Persistence;
using AuthService.Application.Handlers;
using AuthService.Domain.Entities;
using CrossMarket.SharedKernel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AuthHandlersTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher _passwordHasher;
    private readonly Mock<ILogger<RegisterCommandHandler>> _loggerMock;

    public AuthHandlersTests()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AuthDbContext(options);

        var jwtSettings = new JwtSettings
        {
            Issuer        = "TestIssuer",
            Audience      = "TestAudience",
            SecretKey     = "super_secret_key_that_is_at_least_32_chars!",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays   = 30,
        };
        _tokenGenerator = new JwtTokenGenerator(jwtSettings);
        _passwordHasher = new PasswordHasher();
        _loggerMock     = new Mock<ILogger<RegisterCommandHandler>>();
    }

    public void Dispose() => _db.Dispose();

    // ── Register ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterCommand_should_create_user_and_return_tokens()
    {
        var handler = new RegisterCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var result = await handler.Handle(
            new RegisterCommand("newuser@example.com", "Password123", "New User"),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("newuser@example.com");
        result.User.FullName.Should().Be("New User");
    }

    [Fact]
    public async Task RegisterCommand_should_throw_when_email_already_exists()
    {
        await _db.Users.AddAsync(User.Create(
            "existing@example.com", _passwordHasher.HashPassword("pass"), "Existing"));
        await _db.SaveChangesAsync();

        var handler = new RegisterCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var act = () => handler.Handle(
            new RegisterCommand("existing@example.com", "Password123", "New User"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterCommand_should_normalize_email_to_lowercase()
    {
        var handler = new RegisterCommandHandler(_db, _tokenGenerator, _passwordHasher);

        await handler.Handle(
            new RegisterCommand("UPPERCASE@EXAMPLE.COM", "Password123", "Test User"),
            CancellationToken.None);

        var user = await _db.Users.FirstOrDefaultAsync();
        user!.Email.Should().Be("uppercase@example.com");
    }

    // ── Login ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginCommand_should_return_tokens_on_valid_credentials()
    {
        var hash = _passwordHasher.HashPassword("Password123");
        await _db.Users.AddAsync(User.Create("login@example.com", hash, "Login User"));
        await _db.SaveChangesAsync();

        var handler = new LoginCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var result = await handler.Handle(
            new LoginCommand("login@example.com", "Password123"),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginCommand_should_throw_on_unknown_email()
    {
        var handler = new LoginCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var act = () => handler.Handle(
            new LoginCommand("unknown@example.com", "Password123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginCommand_should_throw_on_wrong_password()
    {
        var hash = _passwordHasher.HashPassword("CorrectPassword");
        await _db.Users.AddAsync(User.Create("user@example.com", hash, "User"));
        await _db.SaveChangesAsync();

        var handler = new LoginCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var act = () => handler.Handle(
            new LoginCommand("user@example.com", "WrongPassword"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginCommand_should_throw_when_account_is_suspended()
    {
        var hash = _passwordHasher.HashPassword("Password123");
        var user = User.Create("suspended@example.com", hash, "Suspended");
        user.Suspend();
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        var handler = new LoginCommandHandler(_db, _tokenGenerator, _passwordHasher);

        var act = () => handler.Handle(
            new LoginCommand("suspended@example.com", "Password123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*suspended*");
    }

    // ── Refresh Token ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenCommand_should_issue_new_access_token()
    {
        var hash = _passwordHasher.HashPassword("Password123");
        var user = User.Create("refresh@example.com", hash, "Refresh User");
        var refreshToken = "valid_refresh_token";
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(_db, _tokenGenerator);

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().Be(refreshToken); // token is rotated
    }

    [Fact]
    public async Task RefreshTokenCommand_should_throw_on_expired_token()
    {
        var hash = _passwordHasher.HashPassword("Password123");
        var user = User.Create("expired@example.com", hash, "Expired User");
        user.SetRefreshToken("expired_token", DateTime.UtcNow.AddMinutes(-1));
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(_db, _tokenGenerator);

        var act = () => handler.Handle(
            new RefreshTokenCommand("expired_token"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task RefreshTokenCommand_should_throw_on_unknown_token()
    {
        var handler = new RefreshTokenCommandHandler(_db, _tokenGenerator);

        var act = () => handler.Handle(
            new RefreshTokenCommand("unknown_token"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}