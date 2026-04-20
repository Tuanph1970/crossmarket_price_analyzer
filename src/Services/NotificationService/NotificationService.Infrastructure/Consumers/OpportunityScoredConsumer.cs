using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Infrastructure.Services;

namespace NotificationService.Infrastructure.Consumers;

/// <summary>
/// Consumes OpportunityScoredEvent from RabbitMQ and triggers real-time alert dispatch.
/// Wired in NotificationService.Api/Program.cs via AddMassTransit.
/// </summary>
public sealed class OpportunityScoredConsumer : IConsumer<OpportunityScoredEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpportunityScoredConsumer> _logger;

    public OpportunityScoredConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<OpportunityScoredConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OpportunityScoredEvent> context)
    {
        _logger.LogInformation(
            "Consumed OpportunityScoredEvent for match {MatchId}, score {Score}",
            context.Message.MatchId, context.Message.CompositeScore);

        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<AlertThresholdEngine>();
        await engine.EvaluateThresholdsAsync(context.Message, context.CancellationToken);
    }
}
