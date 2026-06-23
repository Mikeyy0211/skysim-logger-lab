using System.Text.Json;
using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Common;

public class SensitiveDataMasker
{
    private const string MaskedValue = "***";

    public LogEventMessage Mask(LogEventMessage message)
    {
        if (message.RequestData.HasValue)
        {
            message.RequestData = MaskJsonElement(message.RequestData.Value);
        }

        if (message.ResponseData.HasValue)
        {
            message.ResponseData = MaskJsonElement(message.ResponseData.Value);
        }

        return message;
    }

    public string MaskJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        using var doc = JsonDocument.Parse(json);
        var masked = MaskElement(doc.RootElement);
        return masked.GetRawText();
    }

    private static JsonElement MaskJsonElement(JsonElement element)
    {
        var masked = MaskElement(element);
        return masked;
    }

    private static JsonElement MaskElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return MaskObject(element);

            case JsonValueKind.Array:
                return MaskArray(element);

            default:
                return element;
        }
    }

    private static JsonElement MaskObject(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in element.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);

            if (SensitiveFields.Instance.IsSensitive(property.Name))
            {
                writer.WriteStringValue(MaskedValue);
            }
            else
            {
                var masked = MaskElement(property.Value);
                masked.WriteTo(writer);
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
            var masked = MaskElement(item);
            masked.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }
}
