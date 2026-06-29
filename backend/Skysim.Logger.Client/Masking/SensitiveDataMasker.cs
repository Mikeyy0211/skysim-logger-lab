using System.Text.Json;
using Skysim.Logger.Contracts.Constants;

using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Client.Masking;

public class SensitiveDataMasker : ISensitiveDataMasker
{
    private const string MaskedValue = "***";

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        SensitiveFieldNames.Password,
        SensitiveFieldNames.AccessToken,
        SensitiveFieldNames.RefreshToken,
        SensitiveFieldNames.Authorization,
        SensitiveFieldNames.Otp,
        SensitiveFieldNames.CardNumber,
        SensitiveFieldNames.Cvv,
        SensitiveFieldNames.PaymentSecret,
        SensitiveFieldNames.Secret,
        SensitiveFieldNames.Token
    };

    public LogEventMessage Mask(LogEventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var masked = new LogEventMessage
        {
            EventId = message.EventId,
            FlowId = message.FlowId,
            FlowType = message.FlowType,
            CheckoutType = message.CheckoutType,
            UserId = message.UserId,
            CustomerEmail = message.CustomerEmail,
            CustomerPhone = message.CustomerPhone,
            OrderId = message.OrderId,
            PaymentId = message.PaymentId,
            ServiceName = message.ServiceName,
            ActionType = message.ActionType,
            Status = message.Status,
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            CorrelationId = message.CorrelationId,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            Duration = message.Duration,
            CreatedAt = message.CreatedAt,
            Exception = message.Exception
        };

        if (message.RequestData.HasValue)
        {
            masked.RequestData = MaskJsonElement(message.RequestData.Value);
        }

        if (message.ResponseData.HasValue)
        {
            masked.ResponseData = MaskJsonElement(message.ResponseData.Value);
        }

        return masked;
    }

    public string MaskJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var masked = MaskElement(doc.RootElement);
            return masked.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonElement MaskJsonElement(JsonElement element)
    {
        return MaskElement(element);
    }

    private static JsonElement MaskElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => MaskObject(element),
            JsonValueKind.Array => MaskArray(element),
            _ => element
        };
    }

    private static JsonElement MaskObject(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in element.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);

            if (IsSensitive(property.Name))
            {
                writer.WriteStringValue(MaskedValue);
            }
            else
            {
                MaskElement(property.Value).WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    private static JsonElement MaskArray(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var item in element.EnumerateArray())
        {
            MaskElement(item).WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    private static bool IsSensitive(string fieldName)
    {
        return SensitiveFields.Contains(fieldName);
    }
}
