using Skysim.Logger.Contracts.Events;

namespace Skysim.Logger.Client.Producers;

public interface IKafkaLogProducer
{
    Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default);

    Task PublishAsync(object payload, CancellationToken cancellationToken = default);
}