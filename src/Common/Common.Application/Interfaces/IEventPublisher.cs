namespace Common.Application.Interfaces;

/// <summary>
/// Contract for publishing domain events via the message broker.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}
