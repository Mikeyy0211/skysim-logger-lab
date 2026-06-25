using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.Retry;
using Skysim.Logger.Api.Infrastructure.Kafka;

namespace Skysim.Logger.Api.Common;

public static class RetryPolicyFactory
{
    public static ResiliencePipeline CreateDbRetryPolicy(IOptions<KafkaConsumerOptions> options)
    {
        var retryOptions = options.Value.Retry;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxAttempts,
                DelayGenerator = args =>
                {
                    var delay = KafkaCommon.CalculateDelay(args.AttemptNumber + 1, retryOptions);
                    return new ValueTask<TimeSpan?>(delay);
                },
                ShouldHandle = new PredicateBuilder().Handle<NpgsqlException>()
            })
            .Build();
    }

    public static ResiliencePipeline CreateBrokerRetryPolicy(IOptions<KafkaConsumerOptions> options)
    {
        var retryOptions = options.Value.Retry;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxAttempts,
                DelayGenerator = args =>
                {
                    var delay = KafkaCommon.CalculateDelay(args.AttemptNumber + 1, retryOptions);
                    return new ValueTask<TimeSpan?>(delay);
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<ConsumeException>()
                    .Handle<ProduceException<byte[], byte[]>>()
            })
            .Build();
    }
}
