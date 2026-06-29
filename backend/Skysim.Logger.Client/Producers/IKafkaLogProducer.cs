using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Client.Producers;

public interface IKafkaLogProducer
{
    Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default);
}
