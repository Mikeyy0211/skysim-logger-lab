using Confluent.Kafka;
using Skysim.Logger.Contracts.Kafka;

namespace Skysim.Logger.Api.Kafka;

public static class KafkaCommon
{
    public static Acks ParseAcks(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "all" or "-1" => Acks.All,
            "none" or "0" => Acks.None,
            "leader" or "1" => Acks.Leader,
            _ => Acks.All
        };
    }

    public static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        var delay = options.InitialDelayMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, options.MaxDelayMs));
    }
}
