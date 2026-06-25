using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Retry;

namespace Skysim.Logger.Common.Kafka;

public static class RetryPolicyFactory
{
    public static ResiliencePipeline CreateDbRetryPolicy(RetryOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxAttempts,
                DelayGenerator = args =>
                {
                    var delay = KafkaCommon.CalculateDelay(args.AttemptNumber + 1, options);
                    return new ValueTask<TimeSpan?>(delay);
                },
                ShouldHandle = new PredicateBuilder().Handle<NpgsqlException>()
            })
            .Build();
    }

    public static ResiliencePipeline CreateBrokerRetryPolicy(RetryOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxAttempts,
                DelayGenerator = args =>
                {
                    var delay = KafkaCommon.CalculateDelay(args.AttemptNumber + 1, options);
                    return new ValueTask<TimeSpan?>(delay);
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<ConsumeException>()
                    .Handle<ProduceException<byte[], byte[]>>()
            })
            .Build();
    }
}
