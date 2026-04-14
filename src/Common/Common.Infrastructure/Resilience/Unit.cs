// Lightweight Unit to avoid System.Reactive dependency for FallbackPipeline
namespace Common.Infrastructure.Resilience;

public readonly struct Unit
{
    public static Unit Default => default;
}
