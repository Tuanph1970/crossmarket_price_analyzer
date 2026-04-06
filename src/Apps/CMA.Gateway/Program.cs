using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 1. Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2. Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health endpoint
app.MapHealthChecks("/health");

// YARP routes all other requests
app.MapReverseProxy();

app.Run();
