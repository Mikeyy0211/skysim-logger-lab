using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Client.Masking;

public interface ISensitiveDataMasker
{
    string MaskJson(string json);

    LogEventMessage Mask(LogEventMessage message);

    string MaskSensitiveHeader(string headerName, string headerValue);

    Dictionary<string, string> MaskHeaders(Dictionary<string, string>? headers);

    string MaskBody(string? body);
}
