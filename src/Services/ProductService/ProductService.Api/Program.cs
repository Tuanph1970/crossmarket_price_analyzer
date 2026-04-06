using Common.Application.Extensions;
using Common.Infrastructure.Configuration;
using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ProductService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ProductService API", Version = "v1" });
});

// 2. Add infrastructure (DB, Redis, RabbitMQ, OpenTelemetry, Serilog)
builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "ProductService");

// 3. Add application layer (MediatR, validators, pipeline behaviors)
builder.Services.AddCommonApplication();

// 4. Register ProductDbContext
builder.Services.AddDbContext<ProductDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

var app = builder.Build();

// 5. Apply migrations on startup (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await db.Database.MigrateAsync();
}

// 6. Configure pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ProductService", Timestamp = DateTime.UtcNow }));

app.Run();
