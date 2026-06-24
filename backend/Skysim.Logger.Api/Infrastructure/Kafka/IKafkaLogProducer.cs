using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public interface IKafkaLogProducer
{
    Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default);
}
