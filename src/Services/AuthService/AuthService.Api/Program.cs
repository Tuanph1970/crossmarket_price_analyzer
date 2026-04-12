using AuthService.Application.Commands;
using AuthService.Application.DTOs;
using AuthService.Application.Persistence;
using CrossMarket.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── JWT settings ───────────────────────────────────────────────────────────────
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection(JwtSettings.SectionKey).Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);

// ── Infrastructure ─────────────────────────────────────────────────────────────
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "AuthService");
builder.Services.AddCommonApplication();

// ── Auth services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtTokenGenerator>();
builder.Services.AddSingleton<PasswordHasher>();

// ── DbContext ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true;
            ValidateAudience = true;
            ValidateLifetime = true;
            ValidateIssuerSigningKey = true;
            ValidIssuer = jwtSettings.Issuer;
            ValidAudience = jwtSettings.Audience;
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Allow SignalR WebSocket connections with JWT
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/ws/notifications"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AuthService API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// ── Auth endpoints ─────────────────────────────────────────────────────────────

app.MapPost("/api/auth/register", async (
    RegisterRequest req,
    IMediator mediator,
    CancellationToken ct) =>
{
    try
    {
        var result = await mediator.Send(new RegisterCommand(req.Email, req.Password, req.FullName), ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { Error = ex.Message });
    }
})
.Produces<AuthResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status409Conflict)
.WithTags("Auth")
.WithName("Register")
.WithDescription("Registers a new user and returns JWT + refresh tokens.");

// P4-B02: Login
app.MapPost("/api/auth/login", async (
    LoginRequest req,
    IMediator mediator,
    CancellationToken ct) =>
{
    try
    {
        var result = await mediator.Send(new LoginCommand(req.Email, req.Password), ct);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Unauthorized();
    }
})
.Produces<AuthResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.WithTags("Auth")
.WithName("Login")
.WithDescription("Authenticates a user and returns JWT + refresh tokens.");

// P4-B02: Refresh token
app.MapPost("/api/auth/refresh", async (
    RefreshTokenRequest req,
    IMediator mediator,
    CancellationToken ct) =>
{
    try
    {
        var result = await mediator.Send(new RefreshTokenCommand(req.RefreshToken), ct);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
})
.Produces<AuthResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.WithTags("Auth")
.WithName("RefreshToken")
.WithDescription("Exchanges a refresh token for a new JWT access token.");

// ── Watchlist endpoints (auth required) ──────────────────────────────────────

app.MapGet("/api/watchlist", async (
    ClaimsPrincipal user,
    IMediator mediator,
    int page = 1,
    int pageSize = 20,
    CancellationToken ct = default) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var result = await mediator.Send(new GetWatchlistQuery(userId.Value, page, pageSize), ct);
    return Results.Ok(result);
})
.RequireAuthorization()
.Produces<PagedResult<WatchlistItemDto>>(StatusCodes.Status200OK)
.WithTags("Watchlist")
.WithName("GetWatchlist")
.WithDescription("Returns the authenticated user's watchlist.");

// P4-B03: Add to watchlist
app.MapPost("/api/watchlist", async (
    AddToWatchlistRequest req,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var result = await mediator.Send(new AddToWatchlistCommand(
        userId.Value, req.MatchId, req.UsProductName, req.VnProductName,
        req.AlertAboveScore, req.AlertBelowScore), ct);
    return Results.Created($"/api/watchlist/{result.Id}", result);
})
.RequireAuthorization()
.Produces<WatchlistItemDto>(StatusCodes.Status201Created)
.WithTags("Watchlist")
.WithName("AddToWatchlist")
.WithDescription("Adds a product match to the authenticated user's watchlist.");

// P4-B03: Remove from watchlist
app.MapDelete("/api/watchlist/{itemId:guid}", async (
    Guid itemId,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var removed = await mediator.Send(new RemoveFromWatchlistCommand(userId.Value, itemId), ct);
    return removed ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Watchlist")
.WithName("RemoveFromWatchlist")
.WithDescription("Removes an item from the authenticated user's watchlist.");

// ── Alert threshold endpoints (auth required) ─────────────────────────────────

// P4-B07: Get alert thresholds
app.MapGet("/api/alerts/thresholds", async (
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var result = await mediator.Send(new GetAlertThresholdsQuery(userId.Value), ct);
    return Results.Ok(result);
})
.RequireAuthorization()
.Produces<IReadOnlyList<AlertThresholdDto>>(StatusCodes.Status200OK)
.WithTags("AlertThresholds")
.WithName("GetAlertThresholds")
.WithDescription("Returns all alert thresholds for the authenticated user.");

// P4-B07: Create alert threshold
app.MapPost("/api/alerts/thresholds", async (
    CreateAlertThresholdRequest req,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var result = await mediator.Send(new CreateAlertThresholdCommand(
        userId.Value, req.Name, req.MinScore, req.MaxScore,
        req.MinMarginPct, req.MatchId), ct);
    return Results.Created($"/api/alerts/thresholds/{result.Id}", result);
})
.RequireAuthorization()
.Produces<AlertThresholdDto>(StatusCodes.Status201Created)
.WithTags("AlertThresholds")
.WithName("CreateAlertThreshold")
.WithDescription("Creates a new alert threshold for the authenticated user.");

// P4-B07: Update alert threshold
app.MapPut("/api/alerts/thresholds/{thresholdId:guid}", async (
    Guid thresholdId,
    UpdateAlertThresholdRequest req,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    try
    {
        var result = await mediator.Send(new UpdateAlertThresholdCommand(
            userId.Value, thresholdId, req.MinScore, req.MaxScore, req.MinMarginPct), ct);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
})
.RequireAuthorization()
.Produces<AlertThresholdDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("AlertThresholds")
.WithName("UpdateAlertThreshold")
.WithDescription("Updates an existing alert threshold.");

// P4-B07: Delete alert threshold
app.MapDelete("/api/alerts/thresholds/{thresholdId:guid}", async (
    Guid thresholdId,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = GetUserId(user);
    if (userId == null) return Results.Unauthorized();

    var deleted = await mediator.Send(new DeleteAlertThresholdCommand(userId.Value, thresholdId), ct);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.WithTags("AlertThresholds")
.WithName("DeleteAlertThreshold")
.WithDescription("Deactivates an alert threshold.");

// ── Health ──────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "AuthService", Timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .WithName("HealthCheck")
   .WithDescription("Returns the health status of the AuthService.");

app.Run();

// ── Helpers ────────────────────────────────────────────────────────────────────

static Guid? GetUserId(ClaimsPrincipal user)
{
    var uid = user.FindFirst("uid")?.Value
              ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(uid, out var id) ? id : null;
}
