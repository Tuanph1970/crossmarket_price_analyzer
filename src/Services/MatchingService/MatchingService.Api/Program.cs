using Common.Application.Extensions;
using Common.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MatchingService API", Version = "v1" });
});

builder.Host.UseCommonLogging();
builder.Services.AddCommonInfrastructure(builder.Configuration, "MatchingService");
builder.Services.AddCommonApplication();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "MatchingService", Timestamp = DateTime.UtcNow }));

app.Run();
