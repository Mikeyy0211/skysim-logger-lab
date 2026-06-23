using Confluent.Kafka;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public interface IDlqPublisher
{
    Task PublishAsync(
        ConsumeResult<byte[], byte[]> originalResult,
        string failureReason,
        int attempt,
        CancellationToken cancellationToken = default);
}
