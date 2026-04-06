using Common.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.Messaging;

/// <summary>
/// MassTransit/RabbitMQ implementation of IEventPublisher.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        _logger.LogInformation(
            "Publishing event {EventType} from {Publisher}",
            typeof(T).Name,
            GetType().Name);

        await _publishEndpoint.Publish(@event, ct);
    }
}
