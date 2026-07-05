using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Api.Auth;

public interface IJwtAuthContextExtractor
{
    void Extract(LogEventMessage message);
}
