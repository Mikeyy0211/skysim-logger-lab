using System.Text.Json.Serialization;

namespace Skysim.Logger.Api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DetailType
{
    Request,
    Response,
    Error,
    Metadata
}
